using System.Numerics;
using ContentEntity = Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content;

public sealed class EntityFactory {
    public World.Entity Create(ContentEntity.EntityDefinition definition, World.World world, Vector3 position) {
        var entity = world.Spawn(position);

        foreach (var componentDefinition in definition.Components) {
            entity.AddComponent(CreateComponent(componentDefinition));
        }

        return entity;
    }

    private static IComponent CreateComponent(ContentEntity.EntityComponentDefinition definition) {
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
                // Randomized per entity so a whole group doesn't move in lockstep:
                // some circle clockwise, some counter-clockwise, each with its own wander heading.
                CircleDirection = Random.Shared.NextDouble() < 0.5 ? -1f : 1f,
                WanderAngle = (float)(Random.Shared.NextDouble() * Math.Tau)
            },
            _ => throw new NotSupportedException($"Unknown component definition {definition.GetType()}")
        };
    }
}
