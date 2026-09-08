using System.Diagnostics;

namespace Yggdrasilnet.Server;

public sealed class GameLoop(NetServer netServer, Simulation.Simulation simulation, int tickRate) {
    public void Run(CancellationToken cancellationToken) {
        var deltaTime = 1f / tickRate;
        var tickIntervalMs = 1000L / tickRate;

        var stopwatch = Stopwatch.StartNew();
        var nextTickTime = stopwatch.ElapsedMilliseconds;

        while (!cancellationToken.IsCancellationRequested) {
            var now = stopwatch.ElapsedMilliseconds;

            if (now >= nextTickTime) {
                netServer.Poll();
                simulation.Update(deltaTime);

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
