using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System;

public class PlayerMovementSystem : ISystem {
    public void Update(World world, float deltaTime) {
        foreach (var (entity, input) in world.Query<InputComponent>()) {
            if (!entity.TryGetComponent<SteeringComponent>(out var steering)) {
                continue;
            }

            var dir = new Vector2(input.MoveX, input.MoveZ);

            if (dir.LengthSquared() > 1f)
                dir = Vector2.Normalize(dir);

            steering.InputDirection = dir;
            steering.MoveSpeed = input.Speed;
        }
    }
}