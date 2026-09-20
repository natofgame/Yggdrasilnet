using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.Managers;

public sealed class EntityQueryCache {
    private readonly Dictionary<Type, QueryCache> _queryCaches = new();
    private Entity[]? _queryEntities;

    public IEnumerable<(Entity Entity, T Component)> Query<T>(EntityManager entityManager) where T : class, IComponent {
        var cache = GetQueryCache<T>(entityManager);
        if (cache.Matches is null) {
            foreach (var entity in entityManager.Entities) {
                if (entity.TryGetComponent<T>(out var component)) {
                    yield return (entity, component);
                }
            }
            yield break;
        }

        var spawnVersion = entityManager.SpawnVersion;
        var matchIndex = 0;
        var entityIndex = 0;
        while (true) {
            if (spawnVersion != entityManager.SpawnVersion) {
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
            }

            if (cache.IsValid) {
                if (matchIndex == cache.Matches.Length) {
                    yield break;
                }
                entityIndex = cache.Matches[matchIndex++];
            } else if (entityIndex == cache.Entities.Length) {
                yield break;
            }

            var entity = cache.Entities[entityIndex++];
            if (!cache.IsValid && !entityManager.Contains(entity.Id)) {
                continue;
            }
            if (entity.TryGetComponent<T>(out var component)) {
                yield return (entity, component);
            }
        }
    }

    private QueryCache GetQueryCache<T>(EntityManager entityManager) where T : class, IComponent {
        if (_queryCaches.TryGetValue(typeof(T), out var cache)) {
            return cache;
        }

        var entities = _queryEntities ??= entityManager.Entities.ToArray();
        var matches = new List<int>();
        for (var i = 0; i < entities.Length; i++) {
            if (entities[i].HasComponent<T>()) {
                matches.Add(i);
                if (matches.Count > entities.Length / 2) {
                    cache = new QueryCache(entities, null);
                    _queryCaches.Add(typeof(T), cache);
                    return cache;
                }
            }
        }

        cache = new QueryCache(entities, matches.ToArray());
        _queryCaches.Add(typeof(T), cache);
        return cache;
    }

    public void Invalidate(Type type) {
        if (_queryCaches.Remove(type, out var cache)) {
            cache.IsValid = false;
        }
    }

    public void InvalidateAll() {
        foreach (var cache in _queryCaches.Values) {
            cache.IsValid = false;
        }
        _queryCaches.Clear();
        _queryEntities = null;
    }

    private sealed class QueryCache(Entity[] entities, int[]? matches) {
        public Entity[] Entities { get; } = entities;
        public int[]? Matches { get; } = matches;
        public bool IsValid { get; set; } = true;
    }
}
