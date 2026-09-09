using System.Diagnostics;
using LiteNetLib;
using LiteNetLib.Utils;
using Serilog;
using Yggdrasilnet.Shared.Network.Packet.Packets;
using Yggdrasilnet.Shared.Network.Packet.Snapshot;

// ReSharper disable once RedundantUsingDirective
using System.Linq;

namespace Yggdrasilnet.Server;

public sealed class GameLoop(NetServer netServer, Simulation.Simulation simulation, int tickRate) {
    private const long StatsLogIntervalMs = 2000;
    private const int MaxDetailedPlayerLogs = 8;
    private const int SnapshotChunkTargetBytes = 1000;
    private const int SnapshotChunkHeaderBytes = 10;
    private const int SnapshotBaseEntityBytes = 22;
    private const int SnapshotVelocityComponentBytes = 13;
    private uint _snapshotFrameId;

    public void Run(CancellationToken cancellationToken) {
        var deltaTime = 1f / tickRate;
        var tickIntervalMs = 1000L / tickRate;

        var stopwatch = Stopwatch.StartNew();
        var nextTickTime = stopwatch.ElapsedMilliseconds;

        var nextStatsLogTime = stopwatch.ElapsedMilliseconds + StatsLogIntervalMs;
        var ticksSinceLastLog = 0;

        var tickTimeTotalMs = 0.0;
        var tickTimeMaxMs = 0.0;
        var systemTimeTotalsMs = new Dictionary<string, double>();
        var tickStopwatch = new Stopwatch();

        while (!cancellationToken.IsCancellationRequested) {
            var now = stopwatch.ElapsedMilliseconds;

            if (now >= nextTickTime) {
                tickStopwatch.Restart();
                netServer.Poll();
                simulation.Update(deltaTime);
                BroadcastSnapshot();
                var tickMs = tickStopwatch.Elapsed.TotalMilliseconds;

                ticksSinceLastLog++;
                tickTimeTotalMs += tickMs;
                tickTimeMaxMs = Math.Max(tickTimeMaxMs, tickMs);
                foreach (var (systemName, systemMs) in simulation.World.LastSystemTimingsMs) {
                    systemTimeTotalsMs[systemName] = systemTimeTotalsMs.GetValueOrDefault(systemName) + systemMs;
                }

                nextTickTime += tickIntervalMs;
                if (stopwatch.ElapsedMilliseconds > nextTickTime + tickIntervalMs) {
                    nextTickTime = stopwatch.ElapsedMilliseconds;
                }
            } else {
                var sleep = nextTickTime - stopwatch.ElapsedMilliseconds;
                if (sleep > 1) {
                    Thread.Sleep(1);
                }
            }

            now = stopwatch.ElapsedMilliseconds;
            if (now >= nextStatsLogTime) {
                var elapsedSeconds = (now - (nextStatsLogTime - StatsLogIntervalMs)) / 1000f;
                LogStats(ticksSinceLastLog, elapsedSeconds, tickTimeTotalMs, tickTimeMaxMs, systemTimeTotalsMs);
                ticksSinceLastLog = 0;
                tickTimeTotalMs = 0.0;
                tickTimeMaxMs = 0.0;
                systemTimeTotalsMs.Clear();
                nextStatsLogTime = now + StatsLogIntervalMs;
            }
        }
    }

    private void LogStats(int ticksSinceLastLog, float elapsedSeconds, double tickTimeTotalMs, double tickTimeMaxMs,
        Dictionary<string, double> systemTimeTotalsMs) {
        var actualTps = elapsedSeconds > 0 ? ticksSinceLastLog / elapsedSeconds : 0f;
        var sessions = simulation.Sessions.All;
        var avgTickMs = ticksSinceLastLog > 0 ? tickTimeTotalMs / ticksSinceLastLog : 0.0;
        var budgetMs = 1000.0 / tickRate;

        Log.Information(
            "Server status: {ActualTps:F1}/{TargetTps} tps, {EntityCount} entities, {PlayerCount} player(s), tick avg {AvgTickMs:F2}ms / max {MaxTickMs:F2}ms (budget {BudgetMs:F2}ms)",
            actualTps, tickRate, simulation.World.Entities.Count, sessions.Count, avgTickMs, tickTimeMaxMs, budgetMs);

        if (ticksSinceLastLog > 0) {
            foreach (var (systemName, totalMs) in systemTimeTotalsMs.OrderByDescending(kv => kv.Value)) {
                Log.Information("  {SystemName}: avg {AvgMs:F3}ms/tick", systemName, totalMs / ticksSinceLastLog);
            }
        }

        if (sessions.Count <= MaxDetailedPlayerLogs) {
            foreach (var session in sessions) {
                Log.Information("  Player {PlayerId} (entity {EntityId}) - {EndPoint} - ping {Ping} ms",
                    session.Id, session.EntityId, session.Peer.Address, session.Peer.Ping);
            }
        } else {
            var avgPing = sessions.Average(s => s.Peer.Ping);
            var maxPing = sessions.Max(s => s.Peer.Ping);
            Log.Information("  Players: {PlayerCount} connected, ping avg {AvgPing:F1} ms / max {MaxPing} ms",
                sessions.Count, avgPing, maxPing);
        }

        BroadcastStats(actualTps, sessions.Count, (float)avgTickMs, (float)tickTimeMaxMs, (float)budgetMs);
    }

    private void BroadcastStats(float actualTps, int playerCount, float avgTickMs, float maxTickMs, float budgetMs) {
        var sessions = simulation.Sessions.All;
        if (sessions.Count == 0) {
            return;
        }

        var stats = new StatsPacket {
            ActualTps = actualTps,
            TargetTps = tickRate,
            EntityCount = simulation.World.Entities.Count,
            PlayerCount = playerCount,
            AvgTickMs = avgTickMs,
            MaxTickMs = maxTickMs,
            BudgetMs = budgetMs,
        };
        foreach (var session in sessions) {
            netServer.Send(session.Peer, stats, DeliveryMethod.Unreliable);
        }
    }

    private void BroadcastSnapshot() {
        var sessions = simulation.Sessions.All;
        if (sessions.Count == 0) {
            return;
        }

        var frameId = unchecked(++_snapshotFrameId);
        foreach (var session in sessions) {
            var snapshot = simulation.BuildSnapshotForSession(session, tickRate);
            if (snapshot.Entities.Count == 0) {
                continue;
            }

            foreach (var chunk in CreateSnapshotChunks(snapshot.Entities, frameId)) {
                netServer.Send(session.Peer, chunk, DeliveryMethod.Sequenced);
            }
        }
    }

    private static IEnumerable<SnapshotChunkPacket> CreateSnapshotChunks(List<EntitySnapshot> entities, uint frameId) {
        if (entities.Count == 0) {
            yield break;
        }

        var currentChunk = new SnapshotChunkPacket {
            FrameId = frameId,
            ChunkIndex = 0,
        };
        var currentBytes = SnapshotChunkHeaderBytes;

        foreach (var entity in entities) {
            var entityBytes = EstimateEntityBytes(entity);
            var wouldOverflow = currentChunk.Entities.Count > 0
                                && currentBytes + entityBytes > SnapshotChunkTargetBytes;
            if (wouldOverflow) {
                yield return currentChunk;
                currentChunk = new SnapshotChunkPacket {
                    FrameId = frameId,
                    ChunkIndex = (ushort)(currentChunk.ChunkIndex + 1),
                };
                currentBytes = SnapshotChunkHeaderBytes;
            }

            currentChunk.Entities.Add(entity);
            currentBytes += entityBytes;
        }

        currentChunk.IsLastChunk = true;
        yield return currentChunk;
    }

    private static int EstimateEntityBytes(EntitySnapshot entity) {
        var size = SnapshotBaseEntityBytes;
        foreach (var component in entity.Components) {
            size += component.Type switch {
                NetworkedComponentType.Velocity => SnapshotVelocityComponentBytes,
                _ => 0
            };
        }

        return size;
    }
}
