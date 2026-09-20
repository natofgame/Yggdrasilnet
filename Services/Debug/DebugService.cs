using System.Diagnostics;
using System.Text;
using LiteNetLib;
using Serilog;
using Yggdrasilnet.Network.Packet.Packets;
using Yggdrasilnet.Server.Services.Debug.Metric;
using Yggdrasilnet.Server.Simulation.Session;
using Yggdrasilnet.Server.Utils;
using static Yggdrasilnet.Server.Utils.ConsoleFormat;

namespace Yggdrasilnet.Server.Services.Debug;

public sealed class DebugService {
    private const long StatsLogIntervalMs = 2000;
    private const int MaxSystemsShown = 5;
    private const double NegligibleMs = 0.01;

    private readonly NetServer _netServer;
    private readonly Simulation.Simulation _simulation;
    private readonly int _tickRate;
    private readonly GameLoopMetrics _metrics = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private long _nextStatsLogTime;
    private int _ticksSinceLastLog;

    public DebugService(NetServer netServer, Simulation.Simulation simulation, int tickRate) {
        _netServer = netServer;
        _simulation = simulation;
        _tickRate = tickRate;
        _nextStatsLogTime = _stopwatch.ElapsedMilliseconds + StatsLogIntervalMs;
    }

    public void Attach(GameLoop gameLoop) => gameLoop.Ticked += OnTick;

    public void Detach(GameLoop gameLoop) => gameLoop.Ticked -= OnTick;

    private void OnTick(TickMetrics tick) {
        _ticksSinceLastLog++;
        _metrics.RecordTick(
            tick.TickMs,
            tick.PollMs,
            tick.SimulationMs,
            tick.SnapshotBroadcastMs,
            tick.SystemTimingsMs,
            tick.SnapshotMetrics
        );

        var now = _stopwatch.ElapsedMilliseconds;
        if (now < _nextStatsLogTime) {
            return;
        }

        var elapsedSeconds = (now - (_nextStatsLogTime - StatsLogIntervalMs)) / 1000f;
        LogStats(_ticksSinceLastLog, elapsedSeconds);
        _ticksSinceLastLog = 0;
        _nextStatsLogTime = now + StatsLogIntervalMs;
    }

    private void LogStats(int ticksSinceLastLog, float elapsedSeconds) {
        var actualTps = elapsedSeconds > 0 ? ticksSinceLastLog / elapsedSeconds : 0f;
        var sessions = _simulation.Sessions.All;
        var budgetMs = 1000.0 / _tickRate;
        var netMetrics = _netServer.CollectAndResetMetrics();
        var metrics = _metrics.BuildReportAndReset(ticksSinceLastLog, netMetrics);

        Log.Information(BuildDashboard(ticksSinceLastLog, actualTps, sessions, budgetMs, metrics));

        // Détail par joueur : bruit peu pertinent en usage courant, disponible en abaissant le niveau de log à Debug.
        foreach (var session in sessions) {
            Log.Debug("  Player {PlayerId} (entity {EntityId}) - {EndPoint} - ping {Ping} ms",
                session.Id, session.EntityId, session.Peer.Address, session.Peer.Ping);
        }

        BroadcastStats(actualTps, sessions.Count, (float)metrics.AvgTickMs, (float)metrics.MaxTickMs, (float)budgetMs);
    }

    private string BuildDashboard(
        int ticksSinceLastLog,
        float actualTps,
        IReadOnlyCollection<PlayerSession> sessions,
        double budgetMs,
        GameLoopMetricsReport metrics
    ) {
        var sb = new StringBuilder();

        var tickText = ColorByBudget($"{metrics.AvgTickMs:F2}", metrics.AvgTickMs, budgetMs);
        var maxTickText = ColorByBudget($"{metrics.MaxTickMs:F2}", metrics.MaxTickMs, budgetMs);
        var status = metrics.MaxTickMs >= budgetMs ? Red("CRIT") : metrics.AvgTickMs >= budgetMs * 0.75 ? Yellow("WARN") : Green("OK");

        sb.Append(Bold("Server")).Append("  ")
          .Append(Cyan($"{actualTps:F1}/{_tickRate} tps")).Append("  |  ")
          .Append($"{_simulation.World.Entities.Count} entities").Append("  |  ")
          .Append($"{sessions.Count} player(s)").Append("  |  ")
          .Append($"tick {tickText}/{maxTickText}ms").Append(" (budget ").Append($"{budgetMs:F2}ms").Append(")  [").Append(status).Append(']');

        AppendTopSystems(sb, ticksSinceLastLog, metrics);

        sb.Append('\n').Append(Dim("  net")).Append("      ")
          .Append($"↑ {metrics.NetIo.SendCalls} pkt / {FormatBytes(metrics.NetIo.SendBytes)} ({metrics.NetIo.SendSerializeMs + metrics.NetIo.SendSocketMs:F2}ms)")
          .Append("   ")
          .Append($"↓ {metrics.NetIo.ReceiveCalls} pkt / {FormatBytes(metrics.NetIo.ReceiveBytes)} ({metrics.NetIo.ReceiveDecodeMs:F2}ms)")
          .Append("   ")
          .Append($"snapshot {metrics.AvgSnapshotEntities:F0} ent / {metrics.AvgSnapshotChunks:F1} chunks per tick");

        AppendGcLine(sb, metrics);
        AppendPlayersLine(sb, sessions);

        return sb.ToString();
    }

    private static void AppendTopSystems(StringBuilder sb, int ticksSinceLastLog, GameLoopMetricsReport metrics) {
        if (ticksSinceLastLog <= 0) {
            return;
        }

        var topSystems = metrics.SystemTimeTotalsMs
            .Select(kv => (Name: kv.Key, AvgMs: kv.Value / ticksSinceLastLog))
            .Where(s => s.AvgMs >= NegligibleMs) // masque les systèmes dont le coût est insignifiant
            .OrderByDescending(s => s.AvgMs)
            .Take(MaxSystemsShown)
            .ToList();

        if (topSystems.Count == 0) {
            return;
        }

        sb.Append('\n').Append(Dim("  systems")).Append("  ");
        sb.Append(string.Join("   ", topSystems.Select(s => $"{s.Name} {s.AvgMs:F2}ms")));
    }

    private static void AppendGcLine(StringBuilder sb, GameLoopMetricsReport metrics) {
        var allocatedMb = metrics.AllocatedBytes / (1024d * 1024d);
        if (metrics.Gc0Collections == 0 && metrics.Gc1Collections == 0 && metrics.Gc2Collections == 0 && allocatedMb < 0.01) {
            return; // rien de pertinent à afficher
        }

        sb.Append('\n').Append(Dim("  gc")).Append("       ")
          .Append($"gen0 {metrics.Gc0Collections}  gen1 {metrics.Gc1Collections}  gen2 {metrics.Gc2Collections}  alloc {allocatedMb:F2} MB");
    }

    private static void AppendPlayersLine(StringBuilder sb, IReadOnlyCollection<PlayerSession> sessions) {
        if (sessions.Count == 0) {
            return;
        }

        var avgPing = sessions.Average(s => s.Peer.Ping);
        var maxPing = sessions.Max(s => s.Peer.Ping);
        sb.Append('\n').Append(Dim("  players")).Append("  ")
          .Append($"{sessions.Count} connected, ping avg {avgPing:F1}ms / max {maxPing}ms");
    }

    private static string FormatBytes(long bytes) => bytes switch {
        >= 1024 * 1024 => $"{bytes / (1024d * 1024d):F2} MB",
        >= 1024 => $"{bytes / 1024d:F1} KB",
        _ => $"{bytes} B",
    };

    private void BroadcastStats(float actualTps, int playerCount, float avgTickMs, float maxTickMs, float budgetMs) {
        var sessions = _simulation.Sessions.All;
        if (sessions.Count == 0) {
            return;
        }

        var stats = new StatsPacket {
            ActualTps = actualTps,
            TargetTps = _tickRate,
            EntityCount = _simulation.World.Entities.Count,
            PlayerCount = playerCount,
            AvgTickMs = avgTickMs,
            MaxTickMs = maxTickMs,
            BudgetMs = budgetMs,
        };
        foreach (var session in sessions) {
            _netServer.Send(session.Peer, stats, DeliveryMethod.Unreliable);
        }
    }
}
