using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System.Physic;

public sealed class MovementSystem : ISystem {
    private const float MinSpeedSq = 0.0001f;

    public void Update(World world, float deltaTime) {
        foreach (var (entity, velocity) in world.Query<VelocityComponent>()) {
            if (entity.HasComponent<ProjectileComponent>()) {
                continue;
            }

            if (entity.TryGetComponent<DirectionComponent>(out var directionComponent)) {
                var horizontalSq = velocity.X * velocity.X + velocity.Z * velocity.Z;
                if (horizontalSq > MinSpeedSq) {
                    var length = MathF.Sqrt(horizontalSq);
                    directionComponent.X = velocity.X / length;
                    directionComponent.Z = velocity.Z / length;
                } 
            }
            
            entity.Position += new Vector3(velocity.X, velocity.Y, velocity.Z) * deltaTime;
        }
    }
}