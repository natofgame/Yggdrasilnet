using Yggdrasilnet.Server.Simulation.Content.Ai;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System.Ai;

public sealed class AiSystem : ISystem {
    public void Update(World world, float deltaTime) {
        if (!float.IsFinite(deltaTime) || deltaTime < 0) {
            return;
        }

        foreach (var (entity, _) in world.Query<AiComponent>().ToArray()) {
            if (!entity.TryGetComponent<HealthComponent>(out var hp) || hp.Current <= 0f) {
                continue;
            }

            AiBrain.Tick(world, entity, deltaTime);
        }
    }
}
