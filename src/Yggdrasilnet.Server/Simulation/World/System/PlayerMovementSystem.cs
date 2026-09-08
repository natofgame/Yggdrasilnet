using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System;

public class PlayerMovementSystem : ISystem {
    public void Update(World world, float deltaTime) {
        foreach (var (entity, input) in world.Query<InputComponent>()) {
            if (!entity.TryGetComponent<VelocityComponent>(out var velocity)) {
                continue;
            }

            var direction = new Vector2(input.MoveX, input.MoveZ);
            if (direction.LengthSquared() > 1f) {
                direction = Vector2.Normalize(direction);
            } else if (direction.LengthSquared() <= 0f) {
                velocity.X = 0f;
                velocity.Z = 0f;
                continue;
            }

            velocity.X = direction.X * input.Speed;
            velocity.Z = direction.Y * input.Speed;

        }
    }
}