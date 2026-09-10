using Yggdrasilnet.Server.Services;
using Yggdrasilnet.Server.Services.Debug.Metric;

namespace Yggdrasilnet.Server.Tests;

public sealed class GameLoopMetricsTests {
    [Fact]
    public void BuildReportAndResetPreservesAccumulatedSystemTotals() {
        var metrics = new GameLoopMetrics();
        metrics.RecordTick(10, 1, 6, 3,
            new Dictionary<string, double> { ["Movement"] = 2, ["Steering"] = 4 },
            new SnapshotBroadcastMetrics { SnapshotEntities = 10, ChunksSent = 2 });
        metrics.RecordTick(20, 3, 12, 5,
            new Dictionary<string, double> { ["Movement"] = 5, ["Snapshot"] = 7 },
            new SnapshotBroadcastMetrics { SnapshotEntities = 20, ChunksSent = 4 });
        var netMetrics = new NetIoMetricsSnapshot(2, 100, 1, 2, 3, 200, 4);

        var report = metrics.BuildReportAndReset(2, netMetrics);

        Assert.Equal(3, report.SystemTimeTotalsMs.Count);
        Assert.Equal(7.0, report.SystemTimeTotalsMs["Movement"]);
        Assert.Equal(4.0, report.SystemTimeTotalsMs["Steering"]);
        Assert.Equal(7.0, report.SystemTimeTotalsMs["Snapshot"]);
        Assert.Equal(15.0, report.AvgTickMs);
        Assert.Equal(20.0, report.MaxTickMs);
        Assert.Equal(2.0, report.AvgPollMs);
        Assert.Equal(9.0, report.AvgSimulationMs);
        Assert.Equal(4.0, report.AvgSnapshotBroadcastMs);
        Assert.Equal(15.0, report.AvgSnapshotEntities);
        Assert.Equal(3.0, report.AvgSnapshotChunks);
        Assert.Equal(netMetrics, report.NetIo);
    }

    [Fact]
    public void RetainedReportIsUnchangedByFutureRecordingAndReset() {
        var metrics = new GameLoopMetrics();
        metrics.RecordTick(10, 1, 6, 3,
            new Dictionary<string, double> { ["Movement"] = 2, ["Steering"] = 4 }, default);
        var firstReport = metrics.BuildReportAndReset(1, default);

        metrics.RecordTick(20, 3, 12, 5,
            new Dictionary<string, double> { ["Movement"] = 5, ["Snapshot"] = 7 }, default);

        Assert.Equal(2, firstReport.SystemTimeTotalsMs.Count);
        Assert.Equal(2.0, firstReport.SystemTimeTotalsMs["Movement"]);
        Assert.Equal(4.0, firstReport.SystemTimeTotalsMs["Steering"]);
        Assert.False(firstReport.SystemTimeTotalsMs.ContainsKey("Snapshot"));

        var secondReport = metrics.BuildReportAndReset(1, default);

        Assert.NotSame(firstReport.SystemTimeTotalsMs, secondReport.SystemTimeTotalsMs);
        Assert.Equal(2, firstReport.SystemTimeTotalsMs.Count);
        Assert.Equal(2.0, firstReport.SystemTimeTotalsMs["Movement"]);
        Assert.Equal(4.0, firstReport.SystemTimeTotalsMs["Steering"]);
        Assert.False(firstReport.SystemTimeTotalsMs.ContainsKey("Snapshot"));
        Assert.Equal(2, secondReport.SystemTimeTotalsMs.Count);
        Assert.Equal(5.0, secondReport.SystemTimeTotalsMs["Movement"]);
        Assert.Equal(7.0, secondReport.SystemTimeTotalsMs["Snapshot"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BuildReportAndResetClearsIntervalValues(int emptyTickCount) {
        var metrics = new GameLoopMetrics();
        metrics.RecordTick(20, 3, 12, 5,
            new Dictionary<string, double> { ["Movement"] = 12 },
            new SnapshotBroadcastMetrics { SnapshotEntities = 20, ChunksSent = 4 });
        metrics.BuildReportAndReset(1, default);

        var emptyReport = metrics.BuildReportAndReset(emptyTickCount, default);

        Assert.Empty(emptyReport.SystemTimeTotalsMs);
        Assert.Equal(0.0, emptyReport.AvgTickMs);
        Assert.Equal(0.0, emptyReport.MaxTickMs);
        Assert.Equal(0.0, emptyReport.AvgPollMs);
        Assert.Equal(0.0, emptyReport.AvgSimulationMs);
        Assert.Equal(0.0, emptyReport.AvgSnapshotBroadcastMs);
        Assert.Equal(0.0, emptyReport.AvgSnapshotEntities);
        Assert.Equal(0.0, emptyReport.AvgSnapshotChunks);

        metrics.RecordTick(10, 1, 6, 3,
            new Dictionary<string, double> { ["Movement"] = 6 },
            new SnapshotBroadcastMetrics { SnapshotEntities = 10, ChunksSent = 2 });
        var nextReport = metrics.BuildReportAndReset(1, default);

        Assert.Single(nextReport.SystemTimeTotalsMs);
        Assert.Equal(6.0, nextReport.SystemTimeTotalsMs["Movement"]);
        Assert.Equal(10.0, nextReport.AvgTickMs);
        Assert.Equal(10.0, nextReport.MaxTickMs);
        Assert.Equal(1.0, nextReport.AvgPollMs);
        Assert.Equal(6.0, nextReport.AvgSimulationMs);
        Assert.Equal(3.0, nextReport.AvgSnapshotBroadcastMs);
        Assert.Equal(10.0, nextReport.AvgSnapshotEntities);
        Assert.Equal(2.0, nextReport.AvgSnapshotChunks);
        Assert.Empty(emptyReport.SystemTimeTotalsMs);
    }
}
