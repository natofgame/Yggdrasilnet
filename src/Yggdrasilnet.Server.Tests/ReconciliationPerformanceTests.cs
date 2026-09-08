using System.Diagnostics;
using Yggdrasilnet.Server.Simulation.World.Component;
using Xunit.Abstractions;

namespace Yggdrasilnet.Server.Tests;

public sealed class ReconciliationPerformanceTests(ITestOutputHelper output) {
    [Fact]
    [Trait("Category", "Performance")]
    public void InputSequenceReconciliation_HasPerformanceScore() {
        const int operations = 1_000_000;
        const int warmupOperations = 100_000;

        var input = new InputComponent();

        RunMixedSequenceWorkload(input, warmupOperations);

        var stopwatch = Stopwatch.StartNew();
        var accepted = RunMixedSequenceWorkload(input, operations);
        stopwatch.Stop();

        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        var opsPerSecond = operations / elapsedSeconds;
        var nsPerOp = stopwatch.Elapsed.TotalMilliseconds * 1_000_000d / operations;

        const double targetOpsPerSecond = 5_000_000d;
        var score = Math.Clamp((opsPerSecond / targetOpsPerSecond) * 100d, 0d, 100d);

        output.WriteLine($"Reconciliation score: {score:F1}/100");
        output.WriteLine($"Ops/sec: {opsPerSecond:N0}");
        output.WriteLine($"ns/op: {nsPerOp:F2}");
        output.WriteLine($"Accepted seq updates: {accepted:N0}/{operations:N0}");

        Assert.True(accepted > 0);
        Assert.True(opsPerSecond > 100_000d);
    }

    private static int RunMixedSequenceWorkload(InputComponent input, int operations) {
        var accepted = 0;
        uint seq = 1000;
        for (var i = 0; i < operations; i++) {
            var selector = i % 3;
            uint candidate;
            if (selector == 0) {
                seq++;
                candidate = seq;        // nouveau paquet
            } else if (selector == 1) {
                candidate = seq;        // doublon
            } else {
                candidate = seq - 1u;   // ancien paquet
            }

            var applied = input.ApplyInput(1f, 0f, candidate);
            if (applied) {
                accepted++;
            }
        }

        return accepted;
    }
}
