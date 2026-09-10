using System.Numerics;
using Yggdrasilnet.Server.Simulation.Content;
using Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.World;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Tests;

public sealed class EntityFactoryTests {
    [Fact]
    public void SteeringComponentUsesSpawnCenterAndRoamRadiusFromDefinition() {
        var definition = new EntityDefinition {
            Id = "crowd-test",
            Components = [
                new VelocityComponentDefinition(),
                new SteeringComponentDefinition {
                    Speed = 2f,
                    AvoidRadius = 1f,
                    RoamRadius = 5f
                }
            ]
        };

        var world = new World();
        var position = new Vector3(12f, 0f, -9f);
        var entity = new EntityFactory().Create(definition, world, position);

        Assert.True(entity.TryGetComponent<SteeringComponent>(out var steering));
        Assert.Equal(5f, steering.RoamRadius);
        Assert.Equal(12f, steering.SpawnX);
        Assert.Equal(-9f, steering.SpawnZ);
    }
}
