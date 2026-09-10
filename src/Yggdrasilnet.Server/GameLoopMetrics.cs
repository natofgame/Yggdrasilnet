namespace Yggdrasilnet.Server;

public sealed class GameLoopMetrics {
    private double _tickTotalMs;
    private double _tickMaxMs;
    private double _pollTotalMs;
    private double _simulationTotalMs;
    private double _snapshotBroadcastTotalMs;
    private readonly Dictionary<string, double> _systemTimeTotalsMs = new();
    private long _snapshotEntities;
    private long _snapshotChunks;
    private long _gc0AtStart;
    private long _gc1AtStart;
    private long _gc2AtStart;
    private long _allocBytesAtStart;

    public GameLoopMetrics() {
        ResetInterval();
    }

    public void RecordTick(double tickMs, double pollMs, double simulationMs, double snapshotBroadcastMs,
        IReadOnlyDictionary<string, double> systemTimingsMs, SnapshotBroadcastMetrics snapshotMetrics) {
        _tickTotalMs += tickMs;
        _tickMaxMs = Math.Max(_tickMaxMs, tickMs);
        _pollTotalMs += pollMs;
        _simulationTotalMs += simulationMs;
        _snapshotBroadcastTotalMs += snapshotBroadcastMs;
        _snapshotEntities += snapshotMetrics.SnapshotEntities;
        _snapshotChunks += snapshotMetrics.ChunksSent;

        foreach (var (systemName, systemMs) in systemTimingsMs) {
            _systemTimeTotalsMs[systemName] = _systemTimeTotalsMs.GetValueOrDefault(systemName) + systemMs;
        }
    }

    public GameLoopMetricsReport BuildReportAndReset(int tickCount, NetIoMetricsSnapshot netMetrics) {
        var avgTickMs = tickCount > 0 ? _tickTotalMs / tickCount : 0.0;
        var avgPollMs = tickCount > 0 ? _pollTotalMs / tickCount : 0.0;
        var avgSimulationMs = tickCount > 0 ? _simulationTotalMs / tickCount : 0.0;
        var avgSnapshotBroadcastMs = tickCount > 0 ? _snapshotBroadcastTotalMs / tickCount : 0.0;
        var avgSnapshotEntities = tickCount > 0 ? _snapshotEntities / (double)tickCount : 0.0;
        var avgSnapshotChunks = tickCount > 0 ? _snapshotChunks / (double)tickCount : 0.0;

        var gc0 = GC.CollectionCount(0);
        var gc1 = GC.CollectionCount(1);
        var gc2 = GC.CollectionCount(2);
        var allocBytes = GC.GetTotalAllocatedBytes(false);

        var report = new GameLoopMetricsReport(
            avgTickMs,
            _tickMaxMs,
            avgPollMs,
            avgSimulationMs,
            avgSnapshotBroadcastMs,
            avgSnapshotEntities,
            avgSnapshotChunks,
            gc0 - _gc0AtStart,
            gc1 - _gc1AtStart,
            gc2 - _gc2AtStart,
            allocBytes - _allocBytesAtStart,
            netMetrics,
            new Dictionary<string, double>(_systemTimeTotalsMs)
        );

        ResetInterval();
        return report;
    }

    private void ResetInterval() {
        _tickTotalMs = 0.0;
        _tickMaxMs = 0.0;
        _pollTotalMs = 0.0;
        _simulationTotalMs = 0.0;
        _snapshotBroadcastTotalMs = 0.0;
        _snapshotEntities = 0;
        _snapshotChunks = 0;
        _systemTimeTotalsMs.Clear();
        _gc0AtStart = GC.CollectionCount(0);
        _gc1AtStart = GC.CollectionCount(1);
        _gc2AtStart = GC.CollectionCount(2);
        _allocBytesAtStart = GC.GetTotalAllocatedBytes(false);
    }
}

public readonly record struct GameLoopMetricsReport(
    double AvgTickMs,
    double MaxTickMs,
    double AvgPollMs,
    double AvgSimulationMs,
    double AvgSnapshotBroadcastMs,
    double AvgSnapshotEntities,
    double AvgSnapshotChunks,
    long Gc0Collections,
    long Gc1Collections,
    long Gc2Collections,
    long AllocatedBytes,
    NetIoMetricsSnapshot NetIo,
    IReadOnlyDictionary<string, double> SystemTimeTotalsMs
);
