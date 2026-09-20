using Yggdrasilnet.Server.Services;

namespace Yggdrasilnet.Server.Utils;

public readonly record struct TickMetrics(
    double TickMs,
    double PollMs,
    double SimulationMs,
    double SnapshotBroadcastMs,
    IReadOnlyDictionary<string, double> SystemTimingsMs,
    SnapshotBroadcastMetrics SnapshotMetrics
);
