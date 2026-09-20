using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Yggdrasilnet.Maths.Collision;
using Yggdrasilnet.Server.Simulation.Content;
using ContentEntity = Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Simulation.World.Managers;
using Yggdrasilnet.Server.Simulation.World.System;
using Yggdrasilnet.Server.Simulation.World.System.Ai;
using Yggdrasilnet.Server.Simulation.World.System.Combat;
using Yggdrasilnet.Server.Simulation.World.System.Physic;
using Yggdrasilnet.Server.Simulation.World.System.Physic.Collision;
using Yggdrasilnet.Server.Simulation.World.System.Steering;

namespace Yggdrasilnet.Server.Simulation.World;

public sealed class World(
    DefinitionRegistry<SpellDefinition>? spellDefinitions = null,
    DefinitionRegistry<ContentEntity.EntityDefinition>? entityDefinitions = null
) {
    private readonly DefinitionRegistry<SpellDefinition> _spellDefinitions = spellDefinitions ?? new DefinitionRegistry<SpellDefinition>();
    private readonly DefinitionRegistry<ContentEntity.EntityDefinition> _entityDefinitions = entityDefinitions ?? new DefinitionRegistry<ContentEntity.EntityDefinition>();
    private readonly EntityManager _entityManager = new();
    private readonly EntityQueryCache _queryCache = new();
    private readonly SystemManager _systemManager = new();
    private Action<Type>? _componentStructureChanged;
    private readonly List<int> _removedEntityIds = [];

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
        _removedEntityIds.Add(entityId);
        return true;
    }

    public IReadOnlyList<int> DrainRemovedEntityIds() {
        var removed = _removedEntityIds.ToArray();
        _removedEntityIds.Clear();
        return removed;
    }

    public bool TryGetEntity(int entityId, [NotNullWhen(true)] out Entity? entity) {
        return _entityManager.TryGetEntity(entityId, out entity);
    }

    public IEnumerable<(Entity Entity, T Component)> Query<T>() where T : class, IComponent {
        return _queryCache.Query<T>(_entityManager);
    }

    public void Update(float deltaTime) {
        foreach (var entity in _entityManager.Entities) {
            entity.PreviousPosition = entity.Position;
        }

        _systemManager.Update(this, deltaTime);
    } 
    
    public List<Entity> QueryBox(
        BoundingBoxes box,
        Func<Entity, CollisionComponent, bool>? filter = null
    ) {
        var results = new List<Entity>();
        CollisionQuery.CollectOverlaps(this, box, results, filter);
        return results;
    }

    public void Load() {
        AddSystem(new PlayerMovementSystem());
        AddSystem(new SteeringSystem());
        AddSystem(new MovementSystem());
        AddSystem(new CollisionSystem());
        

        AddSystem(new AiSystem());
        
        var phaseSystem = new SpellPhaseSystem(_spellDefinitions);
        AddSystem(phaseSystem);
        AddSystem(new SpellCastIntentSystem(_spellDefinitions, phaseSystem));
        AddSystem(new ProjectileSystem(new SpellEffectApplier(), _spellDefinitions));
        
        AddSystem(new DeathSystem());
    }
}
