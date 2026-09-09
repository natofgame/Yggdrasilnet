using System.Diagnostics;
using LiteNetLib;
using Serilog;

namespace Yggdrasilnet.Server;

public sealed class GameLoop(NetServer netServer, Simulation.Simulation simulation, int tickRate) {
    private const long StatsLogIntervalMs = 2000;

    public void Run(CancellationToken cancellationToken) {
        var deltaTime = 1f / tickRate;
        var tickIntervalMs = 1000L / tickRate;

        var stopwatch = Stopwatch.StartNew();
        var nextTickTime = stopwatch.ElapsedMilliseconds;

        var nextStatsLogTime = stopwatch.ElapsedMilliseconds + StatsLogIntervalMs;
        var ticksSinceLastLog = 0;

        while (!cancellationToken.IsCancellationRequested) {
            var now = stopwatch.ElapsedMilliseconds;

            if (now >= nextTickTime) {
                netServer.Poll();
                simulation.Update(deltaTime);
                BroadcastSnapshot();
                ticksSinceLastLog++;

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

        Log.Information("Server status: {ActualTps:F1}/{TargetTps} tps, {PlayerCount} player(s) connected",
            actualTps, tickRate, sessions.Count);

        foreach (var session in sessions) {
            Log.Information("  Player {PlayerId} (entity {EntityId}) - {EndPoint} - ping {Ping} ms",
                session.Id, session.EntityId, session.Peer.Address, session.Peer.Ping);
        }
    }

    private void BroadcastSnapshot() {
        var sessions = simulation.Sessions.All;
        if (sessions.Count == 0) {
            return;
        }

        var snapshot = simulation.BuildSnapshot();
        foreach (var session in sessions) {
            netServer.Send(session.Peer, snapshot, DeliveryMethod.ReliableOrdered);
        }
    }
}
