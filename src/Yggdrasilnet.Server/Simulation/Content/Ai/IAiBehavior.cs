using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai;

public interface IAiBehavior {
    float Score(AiComponent ai);
    void Tick(World.Entity entity, AiComponent ai, SteeringComponent steering, float dt);
}
