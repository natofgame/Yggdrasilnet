using Yggdrasilnet.Server.Simulation.Session;

namespace Yggdrasilnet.Server.Simulation;

public interface ISimulationContext {
    long Tick { get; }
    PlayerSessionRegistry Sessions { get; }
    World.World World { get; }
    void SpawnEntities(string definitionId, int count);
}
