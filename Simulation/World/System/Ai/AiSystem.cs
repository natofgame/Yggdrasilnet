using Yggdrasilnet.Server.Simulation.Content.Ai;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System.Ai;

public sealed class AiSystem : ISystem {
    private readonly AiGroupCoordinator _groups = new();

    public void Update(World world, float deltaTime) {
        if (!float.IsFinite(deltaTime) || deltaTime < 0) {
            return;
        }

        _groups.Update(world, deltaTime);

        foreach (var (entity, _) in world.Query<AiComponent>().ToArray()) {
            if (!entity.TryGetComponent<HealthComponent>(out var hp) || hp.Current <= 0f) {
                continue;
            }

            AiBrain.Tick(world, entity, deltaTime);
        }
    }
}
