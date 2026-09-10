using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System;

public sealed class MovementSystem : ISystem {
    public void Update(World world, float deltaTime) {
        foreach (var (entity, velocity) in world.Query<VelocityComponent>()) {
            entity.Position += new Vector3(velocity.X, velocity.Y, velocity.Z) * deltaTime;
        }
    }
}