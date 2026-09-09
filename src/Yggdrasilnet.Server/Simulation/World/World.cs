using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Simulation.World.System;

namespace Yggdrasilnet.Server.Simulation.World;

public sealed class World {
    private readonly Dictionary<int, Entity> _entities = new();
    private readonly List<ISystem> _systems = new();
    private int _nextEntityId = 1;

    public IReadOnlyCollection<Entity> Entities => _entities.Values;

    public void AddSystem(ISystem system) {
        _systems.Add(system);
    }

    public Entity Spawn(Vector3 position = default) {
        var entity = new Entity(_nextEntityId++, position);
        _entities[entity.Id] = entity;
        return entity;
    }

    public bool Despawn(int entityId) {
        return _entities.Remove(entityId);
    }

    public bool TryGetEntity(int entityId, [NotNullWhen(true)] out Entity? entity) {
        return _entities.TryGetValue(entityId, out entity);
    }

    public IEnumerable<(Entity Entity, T Component)> Query<T>() where T : class, IComponent {
        foreach (var entity in _entities.Values) {
            if (entity.TryGetComponent<T>(out var component)) {
                yield return (entity, component);
            }
        }
    }

    public void Update(float deltaTime) {
        foreach (var system in _systems) {
            system.Update(this, deltaTime);
        }
    }

    public void Load() {
        AddSystem(new PlayerMovementSystem());
        AddSystem(new SteeringSystem());
        AddSystem(new MovementSystem());
    }
}
