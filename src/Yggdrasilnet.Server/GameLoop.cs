using System.Diagnostics;
using LiteNetLib;
using Serilog;
using Yggdrasilnet.Shared.Network.Packet.Packets;

namespace Yggdrasilnet.Server;

public sealed class GameLoop(NetServer netServer, Simulation.Simulation simulation, int tickRate) {
    private const long StatsLogIntervalMs = 2000;
    private const int MaxDetailedPlayerLogs = 8;
    private readonly SnapshotBroadcastService _snapshotBroadcaster = new(netServer, simulation, tickRate);
    private readonly GameLoopMetrics _metrics = new();

    public void Run(CancellationToken cancellationToken) {
        var fixedDeltaTime = 1f / tickRate;
        var minDeltaTime = fixedDeltaTime * 0.5f;
        var maxDeltaTime = fixedDeltaTime * 3f;
        var tickIntervalMs = 1000L / tickRate;

        var stopwatch = Stopwatch.StartNew();
        var nextTickTime = stopwatch.ElapsedMilliseconds;
        var previousSimulationTimeMs = nextTickTime;

        var nextStatsLogTime = stopwatch.ElapsedMilliseconds + StatsLogIntervalMs;
        var ticksSinceLastLog = 0;

        while (!cancellationToken.IsCancellationRequested) {
            var now = stopwatch.ElapsedMilliseconds;

            if (now >= nextTickTime) {
                var tickStart = Stopwatch.GetTimestamp();

                var pollStart = Stopwatch.GetTimestamp();
                netServer.Poll();
                var pollStop = Stopwatch.GetTimestamp();

                var elapsedMs = Math.Max(0L, now - previousSimulationTimeMs);
                var measuredDeltaTime = elapsedMs / 1000f;
                var simulationDeltaTime = Math.Clamp(measuredDeltaTime, minDeltaTime, maxDeltaTime);
                previousSimulationTimeMs = now;

                var simulationStart = Stopwatch.GetTimestamp();
                simulation.Update(simulationDeltaTime);
                var simulationStop = Stopwatch.GetTimestamp();

                var snapshotStart = Stopwatch.GetTimestamp();
                var snapshotMetrics = _snapshotBroadcaster.Broadcast();
                var snapshotStop = Stopwatch.GetTimestamp();

                var tickStop = Stopwatch.GetTimestamp();
                var tickMs = Stopwatch.GetElapsedTime(tickStart, tickStop).TotalMilliseconds;
                var pollMs = Stopwatch.GetElapsedTime(pollStart, pollStop).TotalMilliseconds;
                var simulationMs = Stopwatch.GetElapsedTime(simulationStart, simulationStop).TotalMilliseconds;
                var snapshotMs = Stopwatch.GetElapsedTime(snapshotStart, snapshotStop).TotalMilliseconds;

                ticksSinceLastLog++;
                _metrics.RecordTick(
                    tickMs,
                    pollMs,
                    simulationMs,
                    snapshotMs,
                    simulation.World.LastSystemTimingsMs,
                    snapshotMetrics
                );

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
                LogStats(ticksSinceLastLog, elapsedSeconds);
                ticksSinceLastLog = 0;
                nextStatsLogTime = now + StatsLogIntervalMs;
            }
        }
    }

    private void LogStats(int ticksSinceLastLog, float elapsedSeconds) {
        var actualTps = elapsedSeconds > 0 ? ticksSinceLastLog / elapsedSeconds : 0f;
        var sessions = simulation.Sessions.All;
        var budgetMs = 1000.0 / tickRate;
        var netMetrics = netServer.CollectAndResetMetrics();
        var metrics = _metrics.BuildReportAndReset(ticksSinceLastLog, netMetrics);

        Log.Information(
            "Server status: {ActualTps:F1}/{TargetTps} tps, {EntityCount} entities, {PlayerCount} player(s), tick avg {AvgTickMs:F2}ms / max {MaxTickMs:F2}ms (budget {BudgetMs:F2}ms)",
            actualTps, tickRate, simulation.World.Entities.Count, sessions.Count, metrics.AvgTickMs, metrics.MaxTickMs, budgetMs);

        if (ticksSinceLastLog > 0) {
            foreach (var (systemName, totalMs) in metrics.SystemTimeTotalsMs.OrderByDescending(kv => kv.Value)) {
                Log.Information("  {SystemName}: avg {AvgMs:F3}ms/tick", systemName, totalMs / ticksSinceLastLog);
            }
        }

        Log.Information(
            "  Outside ECS: poll {PollMs:F3}ms/tick, simulation total {SimulationMs:F3}ms/tick, snapshot broadcast {SnapshotMs:F3}ms/tick",
            metrics.AvgPollMs, metrics.AvgSimulationMs, metrics.AvgSnapshotBroadcastMs
        );
        Log.Information(
            "  Snapshot: avg entities {AvgEntities:F1}/tick, avg chunks {AvgChunks:F1}/tick",
            metrics.AvgSnapshotEntities, metrics.AvgSnapshotChunks
        );
        Log.Information(
            "  Network I/O: send {SendCalls} calls, {SendBytes} bytes, serialize {SerializeMs:F2}ms, socket {SocketMs:F2}ms | recv {ReceiveCalls} packets, {ReceiveBytes} bytes, decode {DecodeMs:F2}ms",
            metrics.NetIo.SendCalls,
            metrics.NetIo.SendBytes,
            metrics.NetIo.SendSerializeMs,
            metrics.NetIo.SendSocketMs,
            metrics.NetIo.ReceiveCalls,
            metrics.NetIo.ReceiveBytes,
            metrics.NetIo.ReceiveDecodeMs
        );
        Log.Information(
            "  GC: gen0 {Gc0}, gen1 {Gc1}, gen2 {Gc2}, allocated {AllocatedMB:F2} MB",
            metrics.Gc0Collections,
            metrics.Gc1Collections,
            metrics.Gc2Collections,
            metrics.AllocatedBytes / (1024d * 1024d)
        );

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

        BroadcastStats(actualTps, sessions.Count, (float)metrics.AvgTickMs, (float)metrics.MaxTickMs, (float)budgetMs);
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

}
