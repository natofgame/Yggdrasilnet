using Yggdrasilnet.Server.Simulation.World;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Simulation.World.System;

namespace Yggdrasilnet.Server.Tests;

public sealed class PlayerMovementSystemTests {
    [Fact]
    public void SetsVelocityForUnitInput() {
        var world = new World();
        var entity = world.Spawn();
        entity.AddComponent(new InputComponent { MoveX = 1f, MoveZ = 0f, Speed = 3f });
        entity.AddComponent(new VelocityComponent());

        var system = new PlayerMovementSystem();
        system.Update(world, 1f / 30f);

        Assert.True(entity.TryGetComponent<VelocityComponent>(out var velocity));
        Assert.Equal(3f, velocity.X);
        Assert.Equal(0f, velocity.Z);
    }

    [Fact]
    public void ResetsVelocityWhenNoInput() {
        var world = new World();
        var entity = world.Spawn();
        entity.AddComponent(new InputComponent { MoveX = 0f, MoveZ = 0f, Speed = 3f });
        entity.AddComponent(new VelocityComponent { X = 2f, Z = -1f });

        var system = new PlayerMovementSystem();
        system.Update(world, 1f / 30f);

        Assert.True(entity.TryGetComponent<VelocityComponent>(out var velocity));
        Assert.Equal(0f, velocity.X);
        Assert.Equal(0f, velocity.Z);
    }
}
