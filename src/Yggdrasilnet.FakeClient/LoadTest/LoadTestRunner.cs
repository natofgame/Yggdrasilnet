using System.Linq;
using Serilog;

namespace Yggdrasilnet.FakeClient.LoadTest;

public sealed class LoadTestRunner : IDisposable {
    private readonly LoadTestOptions _options;
    private readonly List<LoadTestClient> _clients = new();

    private int _entitiesRequested;
    private (int Clients, int Entities) _lastStable;

    public LoadTestRunner(LoadTestOptions options) {
        _options = options;
        _entitiesRequested = options.StartEntities;
        _lastStable = (0, 0);
    }

    public void Run(CancellationToken cancellationToken) {
        Log.Information(
            "Starting load test against {Host}:{Port} — clients {StartClients}->{MaxClients} (+{ClientStep}/step), " +
            "entities {StartEntities}->{MaxEntities} (+{EntityStep}/step), step every {StepInterval}s",
            _options.Host, _options.Port, _options.StartClients, _options.MaxClients, _options.ClientStep,
            _options.StartEntities, _options.MaxEntities, _options.EntityStep, _options.StepIntervalSeconds);

        AddClients(_options.StartClients);
        if (_entitiesRequested > 0) {
            RequestEntities(_entitiesRequested);
        }

        var stepInterval = TimeSpan.FromSeconds(_options.StepIntervalSeconds);
        var nextStepAt = DateTime.UtcNow + stepInterval;
        var firstStepDone = false;

        while (!cancellationToken.IsCancellationRequested) {
            foreach (var client in _clients) {
                client.Poll();
            }

            if (DateTime.UtcNow >= nextStepAt) {
                PrintStatus();

                if (TryDetectBreak(firstStepDone, out var reason)) {
                    ReportBreak(reason);
                    return;
                }

                _lastStable = (_clients.Count, _entitiesRequested);
                firstStepDone = true;

                if (_clients.Count < _options.MaxClients) {
                    AddClients(Math.Min(_options.ClientStep, _options.MaxClients - _clients.Count));
                }
                if (_entitiesRequested < _options.MaxEntities) {
                    var toAdd = Math.Min(_options.EntityStep, _options.MaxEntities - _entitiesRequested);
                    RequestEntities(toAdd);
                }

                if (_clients.Count >= _options.MaxClients && _entitiesRequested >= _options.MaxEntities) {
                    Log.Information("Reached configured max clients/entities without breaking. Increase --max-clients/--max-entities to push further.");
                    return;
                }

                nextStepAt = DateTime.UtcNow + stepInterval;
            }

            Thread.Sleep(15);
        }
    }

    private void AddClients(int count) {
        for (var i = 0; i < count; i++) {
            _clients.Add(new LoadTestClient(_options.Host, _options.Port));
        }
    }

    private void RequestEntities(int count) {
        if (count <= 0 || _clients.Count == 0) {
            return;
        }

        _clients[0].RequestSpawnEntities("crowd", count);
        _entitiesRequested += count;
    }

    private void PrintStatus() {
        var connected = _clients.Count(c => c.Peer != null);
        var latencies = _clients.Select(c => c.AverageLatencyMs()).Where(l => l.HasValue).Select(l => l!.Value).ToList();
        var avgLatency = latencies.Count > 0 ? latencies.Average() : (double?)null;
        var maxLatency = latencies.Count > 0 ? latencies.Max() : (double?)null;
        var stats = _clients.Select(c => c.LastStats).LastOrDefault(s => s != null);

        Log.Information(
            "Step: clients {Connected}/{Total}, entities requested {EntitiesRequested} (server reports {ServerEntityCount}), " +
            "client latency avg {AvgLatency}/max {MaxLatency} ms, server tick avg {ServerAvgTick}/max {ServerMaxTick} ms (budget {Budget}ms), tps {Tps}",
            connected, _clients.Count, _entitiesRequested,
            stats?.EntityCount.ToString() ?? "?",
            avgLatency?.ToString("F1") ?? "?", maxLatency?.ToString("F1") ?? "?",
            stats?.AvgTickMs.ToString("F2") ?? "?", stats?.MaxTickMs.ToString("F2") ?? "?", stats?.BudgetMs.ToString("F2") ?? "?",
            stats?.ActualTps.ToString("F1") ?? "?");
    }

    private bool TryDetectBreak(bool firstStepDone, out string reason) {
        if (_clients.Any(c => c.Faulted)) {
            reason = "un client s'est déconnecté de façon inattendue (timeout/refus du serveur)";
            return true;
        }

        if (!firstStepDone) {
            reason = "";
            return false;
        }

        var stats = _clients.Select(c => c.LastStats).LastOrDefault(s => s != null);
        if (stats != null && stats.AvgTickMs > stats.BudgetMs * _options.TickBudgetMultiplier) {
            reason = $"le serveur dépasse son budget de tick ({stats.AvgTickMs:F2}ms avg pour un budget de {stats.BudgetMs:F2}ms) -> tps réel {stats.ActualTps:F1}/{stats.TargetTps}";
            return true;
        }

        var latencies = _clients.Select(c => c.AverageLatencyMs()).Where(l => l.HasValue).Select(l => l!.Value).ToList();
        if (latencies.Count > 0) {
            var avgLatency = latencies.Average();
            if (avgLatency > _options.LatencyThresholdMs) {
                reason = $"latence moyenne client trop élevée ({avgLatency:F1}ms > seuil {_options.LatencyThresholdMs}ms)";
                return true;
            }
        }

        reason = "";
        return false;
    }

    private void ReportBreak(string reason) {
        Log.Warning("=== ARCHITECTURE LIMIT REACHED ===");
        Log.Warning("Raison : {Reason}", reason);
        Log.Warning("Dernier palier stable : {Clients} client(s) connecté(s), {Entities} entités demandées",
            _lastStable.Clients, _lastStable.Entities);
        Log.Warning("=> C'est ta métrique de référence pour comparer avant/après une optimisation.");
    }

    public void Dispose() {
        foreach (var client in _clients) {
            client.Dispose();
        }
    }
}
