using System.Numerics;
using Yggdrasilnet.Server.Simulation.World;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Simulation.World.System;

namespace Yggdrasilnet.Server.Tests;

public sealed class SteeringSystemTests {
    [Fact]
    public void SeeksTowardThePlayerWhenFar() {
        var world = new World();

        var player = world.Spawn(new Vector3(10f, 0f, 0f));
        player.AddComponent(new InputComponent { Speed = 3f });

        var goblin = world.Spawn(Vector3.Zero);
        goblin.AddComponent(new VelocityComponent());
        goblin.AddComponent(new SteeringComponent {
            MoveSpeed = 2.5f,
            AvoidRadius = 1f,
            CircleRadius = 4f,
            CircleWeight = 0f, // isolate the seek behavior
            SeekWeight = 1f
        });

        new SteeringSystem().Update(world, 1f / 30f);

        Assert.True(goblin.TryGetComponent<VelocityComponent>(out var velocity));
        Assert.True(velocity.X > 0f, "Goblin should move toward the player along +X");
        Assert.Equal(0f, velocity.Z, 3);
    }

    [Fact]
    public void AvoidsOtherSteeredEntitiesWhenTooClose() {
        var world = new World();

        var goblinA = world.Spawn(new Vector3(0f, 0f, 0f));
        goblinA.AddComponent(new VelocityComponent());
        goblinA.AddComponent(new SteeringComponent { MoveSpeed = 2.5f, AvoidRadius = 2f, AvoidWeight = 1f });

        var goblinB = world.Spawn(new Vector3(0.5f, 0f, 0f));
        goblinB.AddComponent(new VelocityComponent());
        goblinB.AddComponent(new SteeringComponent { MoveSpeed = 2.5f, AvoidRadius = 2f, AvoidWeight = 1f });

        new SteeringSystem().Update(world, 1f / 30f);

        Assert.True(goblinA.TryGetComponent<VelocityComponent>(out var velocityA));
        Assert.True(velocityA.X < 0f, "GoblinA should steer away from GoblinB (negative X)");
    }

    [Fact]
    public void StaysIdleWithoutATargetOrNeighbors() {
        var world = new World();

        var goblin = world.Spawn(Vector3.Zero);
        goblin.AddComponent(new VelocityComponent { X = 1f, Z = 1f });
        goblin.AddComponent(new SteeringComponent { MoveSpeed = 2.5f, AvoidRadius = 1f, CircleRadius = 4f });

        new SteeringSystem().Update(world, 1f / 30f);

        Assert.True(goblin.TryGetComponent<VelocityComponent>(out var velocity));
        Assert.Equal(0f, velocity.X);
        Assert.Equal(0f, velocity.Z);
    }
}
