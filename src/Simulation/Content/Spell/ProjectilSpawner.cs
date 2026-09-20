using System.Numerics;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions.Projectiles;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Spell;

public static class ProjectileSpawner {
    private const float MinDirectionSq = 0.0001f;

    public static bool TrySpawn(
        World.World world,
        World.Entity caster,
        SpellCastContext context,
        SpellDefinition spell,
        ProjectileDefinition definition) {

        if (!IsValid(context, definition)) {
            return false;
        }

        var spawnPosition = context.Origin
                            + context.Direction * definition.SpawnForward
                            + Vector3.UnitY * definition.SpawnHeight;

        var chain = definition.Chain;

        var entity = world.Spawn(spawnPosition);
        entity.AddComponent(new ProjectileComponent {
            SpellDefinitionId = spell.Id,
            Context = context with { Origin = spawnPosition },
            Direction = context.Direction,
            Speed = definition.Speed,
            MaxLifetimeSeconds = definition.Lifetime,
            HitRadius = definition.HitRadius,
            RemainingHits = definition.MaxHits,
            AimHeight = definition.SpawnHeight,
            TargetLayer = SpellTargetResolver.ResolveEnemyLayer(caster),
            ChainRadius = chain?.Radius ?? 0f,
            BouncesRemaining = chain?.MaxBounces ?? 0,
            ChainDamageMultiplier = chain?.DamageMultiplierPerBounce ?? 1f,
        });
        return true;
    }

    private static bool IsValid(SpellCastContext context, ProjectileDefinition definition) {
        var direction = context.Direction;
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z)) {
            return false;
        }

        if (definition.Chain is { } chain
            && (!float.IsFinite(chain.Radius) || chain.Radius <= 0f
                || chain.MaxBounces < 0
                || !float.IsFinite(chain.DamageMultiplierPerBounce) || chain.DamageMultiplierPerBounce < 0f)) {
            return false;
        }

        return direction.LengthSquared() > MinDirectionSq
               && float.IsFinite(definition.Speed) && definition.Speed > 0f
               && float.IsFinite(definition.Lifetime) && definition.Lifetime > 0f
               && float.IsFinite(definition.HitRadius) && definition.HitRadius > 0f
               && definition.MaxHits >= 1;
    }
}