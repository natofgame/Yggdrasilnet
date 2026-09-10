using System.Diagnostics;
using Yggdrasilnet.Server.Services;
using Yggdrasilnet.Server.Utils;

namespace Yggdrasilnet.Server;

public sealed class GameLoop(NetServer netServer, Simulation.Simulation simulation, int tickRate) {
    private readonly SnapshotBroadcastService _snapshotBroadcaster = new(netServer, simulation, tickRate);

    public event Action<TickMetrics>? Ticked;

    public void Run(CancellationToken cancellationToken) {
        var fixedDeltaTime = 1f / tickRate;
        var minDeltaTime = fixedDeltaTime * 0.5f;
        var maxDeltaTime = fixedDeltaTime * 3f;
        var tickIntervalMs = 1000L / tickRate;

        var stopwatch = Stopwatch.StartNew();
        var nextTickTime = stopwatch.ElapsedMilliseconds;
        var previousSimulationTimeMs = nextTickTime;

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

                Ticked?.Invoke(new TickMetrics(
                    tickMs,
                    pollMs,
                    simulationMs,
                    snapshotMs,
                    simulation.World.LastSystemTimingsMs,
                    snapshotMetrics
                ));

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
        }
    }
}
