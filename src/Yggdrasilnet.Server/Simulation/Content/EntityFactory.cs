using System.Numerics;
using ContentEntity = Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content;

public sealed class EntityFactory {
    public World.Entity Create(ContentEntity.EntityDefinition definition, World.World world, Vector3 position) {
        var entity = world.Spawn(position);

        foreach (var componentDefinition in definition.Components) {
            entity.AddComponent(CreateComponent(componentDefinition, position));
        }

        return entity;
    }

    private static IComponent CreateComponent(ContentEntity.EntityComponentDefinition definition, Vector3 position) {
        return definition switch {
            ContentEntity.VelocityComponentDefinition velocity => new VelocityComponent { X = velocity.Speed },
            ContentEntity.HealthComponentDefinition health => new HealthComponent { Max = health.Max, Current = health.Max },
            ContentEntity.InputComponentDefinition input => new InputComponent { Speed =  input.Speed },
            ContentEntity.SteeringComponentDefinition steering => new SteeringComponent {
                MoveSpeed = steering.Speed,
                AvoidRadius = steering.AvoidRadius,
                AvoidWeight = steering.AvoidWeight,
                SeekWeight = steering.SeekWeight,
                CircleRadius = steering.CircleRadius,
                CircleWeight = steering.CircleWeight,
                WanderWeight = steering.WanderWeight,
                WanderJitter = steering.WanderJitter,
                RoamRadius = MathF.Max(0f, steering.RoamRadius),
                SpawnX = position.X,
                SpawnZ = position.Z,
                CircleDirection = Random.Shared.NextDouble() < 0.5 ? -1f : 1f,
                WanderAngle = (float)(Random.Shared.NextDouble() * Math.Tau)
            },
            _ => throw new NotSupportedException($"Unknown component definition {definition.GetType()}")
        };
    }
}
