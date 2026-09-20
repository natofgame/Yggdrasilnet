using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System;

public sealed class DeathSystem : ISystem {
    private const float DespawnDelay = 0.3f;

    public void Update(World world, float deltaTime) {
        foreach (var (entity, health) in world.Query<HealthComponent>().ToArray()) {
            if (!(health.Current <= 0f)) {
                continue;
            }

            if (!entity.TryGetComponent<DyingComponent>(out var dying)) {
                entity.AddComponent(new DyingComponent { Timer = DespawnDelay });
                continue;
            }

            dying.Timer -= deltaTime;
            if (dying.Timer <= 0f) {
                world.Despawn(entity.Id);
            }
        }
    }
}