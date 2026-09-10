using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Simulation.World.Managers;
using Yggdrasilnet.Server.Simulation.World.System;
using Yggdrasilnet.Server.Simulation.World.System.Steering;

namespace Yggdrasilnet.Server.Simulation.World;

public sealed class World {
    private readonly EntityManager _entityManager = new();
    private readonly EntityQueryCache _queryCache = new();
    private readonly SystemManager _systemManager = new();
    private Action<Type>? _componentStructureChanged;

    public IReadOnlyCollection<Entity> Entities => _entityManager.Entities;

    public IReadOnlyDictionary<string, double> LastSystemTimingsMs => _systemManager.LastSystemTimingsMs;

    public void AddSystem(ISystem system) {
        _systemManager.AddSystem(system);
    }

    public Entity Spawn(Vector3 position = default) {
        var entity = _entityManager.Spawn(position, _componentStructureChanged ??= _queryCache.Invalidate);
        _queryCache.InvalidateAll();
        return entity;
    }

    public bool Despawn(int entityId) {
        if (!_entityManager.Despawn(entityId)) {
            return false;
        }

        _queryCache.InvalidateAll();
        return true;
    }

    public bool TryGetEntity(int entityId, [NotNullWhen(true)] out Entity? entity) {
        return _entityManager.TryGetEntity(entityId, out entity);
    }

    public IEnumerable<(Entity Entity, T Component)> Query<T>() where T : class, IComponent {
        return _queryCache.Query<T>(_entityManager);
    }

    public void Update(float deltaTime) {
        _systemManager.Update(this, deltaTime);
    }

    public void Load() {
        AddSystem(new PlayerMovementSystem());
        AddSystem(new SteeringSystem());
        AddSystem(new MovementSystem());
    }
}
