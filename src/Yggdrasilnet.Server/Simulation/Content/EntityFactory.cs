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
            _ => throw new NotSupportedException($"Unknown component definition {definition.GetType()}")
        };
    }
}
