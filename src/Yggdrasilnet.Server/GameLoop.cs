using System.Diagnostics;
using LiteNetLib;

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
                BroadcastSnapshot();

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

    private void BroadcastSnapshot() {
        var sessions = simulation.Sessions.All;
        if (sessions.Count == 0) {
            return;
        }

        var snapshot = simulation.BuildSnapshot();
        foreach (var session in sessions) {
            netServer.Send(session.Peer, snapshot, DeliveryMethod.Sequenced);
        }
    }
}
