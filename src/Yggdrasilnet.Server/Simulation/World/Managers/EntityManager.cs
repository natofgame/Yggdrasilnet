using System.Numerics;
using System.Diagnostics.CodeAnalysis;

namespace Yggdrasilnet.Server.Simulation.World.Managers;

public sealed class EntityManager {
    private readonly Dictionary<int, Entity> _entities = new();
    private int _nextEntityId = 1;

    public IReadOnlyCollection<Entity> Entities => _entities.Values;
    
    public int SpawnVersion { get; private set; }

    public Entity Spawn(Vector3 position, Action<Type>? componentStructureChanged) {
        var entity = new Entity(_nextEntityId++, position);
        _entities[entity.Id] = entity;
        entity.ComponentStructureChanged = componentStructureChanged;
        SpawnVersion++;
        return entity;
    }

    public bool Despawn(int entityId) {
        if (!_entities.Remove(entityId, out var entity)) {
            return false;
        }

        entity.ComponentStructureChanged = null;
        return true;
    }

    public bool TryGetEntity(int entityId, [NotNullWhen(true)] out Entity? entity) {
        return _entities.TryGetValue(entityId, out entity);
    }

    public bool Contains(int entityId) => _entities.ContainsKey(entityId);
}
