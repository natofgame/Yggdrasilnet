using System.Diagnostics;
using Yggdrasilnet.Server.Simulation.World.System;

namespace Yggdrasilnet.Server.Simulation.World.Managers;

public sealed class SystemManager {
    private readonly List<ISystem> _systems = new();
    private readonly Dictionary<string, double> _lastSystemTimingsMs = new();

    public IReadOnlyDictionary<string, double> LastSystemTimingsMs => _lastSystemTimingsMs;

    public void AddSystem(ISystem system) {
        _systems.Add(system);
    }

    public void Update(World world, float deltaTime) {
        var stopwatch = Stopwatch.StartNew();
        foreach (var system in _systems) {
            stopwatch.Restart();
            system.Update(world, deltaTime);
            _lastSystemTimingsMs[system.GetType().Name] = stopwatch.Elapsed.TotalMilliseconds;
        }
    }
}
