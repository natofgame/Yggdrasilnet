using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Simulation.World.System;

namespace Yggdrasilnet.Server.Simulation.World;

public sealed class World {
    private readonly Dictionary<int, Entity> _entities = new();
    private readonly List<ISystem> _systems = new();
    private readonly Dictionary<string, double> _lastSystemTimingsMs = new();
    private readonly Dictionary<Type, QueryCache> _queryCaches = new();
    private Entity[]? _queryEntities;
    private Action<Type>? _componentStructureChanged;
    private int _spawnVersion;
    private int _nextEntityId = 1;

    public IReadOnlyCollection<Entity> Entities => _entities.Values;

    public IReadOnlyDictionary<string, double> LastSystemTimingsMs => _lastSystemTimingsMs;

    public void AddSystem(ISystem system) {
        _systems.Add(system);
    }

    public Entity Spawn(Vector3 position = default) {
        var entity = new Entity(_nextEntityId++, position);
        _entities[entity.Id] = entity;
        entity.ComponentStructureChanged = _componentStructureChanged ??= InvalidateQuery;
        _spawnVersion++;
        InvalidateQueries();
        return entity;
    }

    public bool Despawn(int entityId) {
        if (!_entities.Remove(entityId, out var entity)) {
            return false;
        }

        entity.ComponentStructureChanged = null;
        InvalidateQueries();
        return true;
    }

    public bool TryGetEntity(int entityId, [NotNullWhen(true)] out Entity? entity) {
        return _entities.TryGetValue(entityId, out entity);
    }

    public IEnumerable<(Entity Entity, T Component)> Query<T>() where T : class, IComponent {
        var cache = GetQueryCache<T>();
        if (cache.Matches is null) {
            // Dense queries keep the original scan rather than paying for an index on every entity.
            foreach (var entity in _entities.Values) {
                if (entity.TryGetComponent<T>(out var component)) {
                    yield return (entity, component);
                }
            }
            yield break;
        }

        var spawnVersion = _spawnVersion;
        var matchIndex = 0;
        var entityIndex = 0;
        while (true) {
            // Dictionary removals are supported during enumeration, but additions invalidate it.
            if (spawnVersion != _spawnVersion) {
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
            // After a mutation, visit all remaining original slots so newly added components are visible.
            if (!cache.IsValid && !_entities.ContainsKey(entity.Id)) {
                continue;
            }
            if (entity.TryGetComponent<T>(out var component)) {
                yield return (entity, component);
            }
        }
    }

    private QueryCache GetQueryCache<T>() where T : class, IComponent {
        if (_queryCaches.TryGetValue(typeof(T), out var cache)) {
            return cache;
        }

        // Capture actual dictionary order, including slots reused after despawns.
        var entities = _queryEntities ??= _entities.Values.ToArray();
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

    private void InvalidateQuery(Type type) {
        if (_queryCaches.Remove(type, out var cache)) {
            cache.IsValid = false;
        }
    }

    private void InvalidateQueries() {
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

    public void Update(float deltaTime) {
        var stopwatch = Stopwatch.StartNew();
        foreach (var system in _systems) {
            stopwatch.Restart();
            system.Update(this, deltaTime);
            _lastSystemTimingsMs[system.GetType().Name] = stopwatch.Elapsed.TotalMilliseconds;
        }
    }

    public void Load() {
        AddSystem(new PlayerMovementSystem());
        AddSystem(new SteeringSystem());
        AddSystem(new MovementSystem());
    }
}
