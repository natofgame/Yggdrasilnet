using Yggdrasilnet.Server.Simulation.Content.Ai;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System.Ai;

public sealed class AiSystem : ISystem {
    private readonly AiIntentPipeline _pipeline = new();
    
    public void Update(World world, float deltaTime) {
        if (!float.IsFinite(deltaTime) || deltaTime < 0) {
            return;
        }
        foreach (var (entity, ai) in world.Query<AiComponent>().ToArray()) {
            if (!entity.TryGetComponent<HealthComponent>(out var hp) || hp.Current <= 0f) {
                continue;
            }

            _pipeline.Tick(world, entity, deltaTime);
        }
    }
}