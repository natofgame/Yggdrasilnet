using System.Numerics;
using ContentEntity = Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Utils;

namespace Yggdrasilnet.Server.Simulation.Content;

public sealed class EntityFactory {
    public World.Entity Create(ContentEntity.EntityDefinition definition, World.World world, Vector3 position, byte definitionIndex = 0) {
        var entity = world.Spawn(position);
        entity.DefinitionIndex = definitionIndex;

        foreach (var componentDefinition in definition.Components) {
            entity.AddComponent(CreateComponent(componentDefinition, position, entity.Id));
        }

        return entity;
    }

    private static IComponent CreateComponent(ContentEntity.EntityComponentDefinition definition, Vector3 position, int entityId) {
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
            ContentEntity.SpellbookComponentDefinition spellbook => new SpellbookComponent { Spells = [.. spellbook.Spells] },
            ContentEntity.CollisionComponentDefinition collision => new CollisionComponent {
                X = collision.X,
                Y = collision.Y,
                Z = collision.Z,
                IsTrigger = collision.IsTrigger,
                Layer = CollisionUtil.GetString(collision.Layer),
                Mask = CollisionUtil.GetString(collision.Mask)
            },
            _ => throw new NotSupportedException($"Unknown component definition {definition.GetType()}")
        };
    }
}
