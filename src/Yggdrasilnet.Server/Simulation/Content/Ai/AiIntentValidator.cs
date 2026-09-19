using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Compatibility;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content.Ai;

public static class AiIntentValidator {
    public static bool TryCreate(World.World world, World.Entity entity, float deltaTime, out AiContext context) {
        context = null!;
        if (!entity.TryGetComponent<HealthComponent>(out var health)) {
            return false;
        }

        var healthRatio = health.Current / health.Max;
        var hasTarget = TryFindNearestEnemy(world, entity, 20f, out var target);

        var targetHealthRatio = 0f;
        var targetDirection = Vector3.Zero;
        var targetDistance = 0f;

        if (hasTarget && target.TryGetComponent<HealthComponent>(out var targetHealth)) {
            targetHealthRatio = targetHealth.Current / targetHealth.Max;

            var toTarget = target.Position - entity.Position;
            toTarget.Y = 0;

            targetDistance = toTarget.Length();
            if (toTarget.LengthSquared() > 0.0001f) {
                targetDirection = Vector3.Normalize(toTarget);
            }
        }

        var isCasting = entity.TryGetComponent<ActionStateComponent>(out var action) && action.Phase != SpellPhase.None;

        if (!entity.TryGetComponent<AiCombatStateComponent>(out var combatState)) {
            combatState = new AiCombatStateComponent();
            entity.AddComponent(combatState);
        }
        if (combatState.PostAttackTimer > 0f) {
            combatState.PostAttackTimer = MathF.Max(0f, combatState.PostAttackTimer - deltaTime);
        }

        context = new AiContext {
            Id = entity.Id,
            Position = entity.Position,
            TargetHealthRatio = targetHealthRatio,
            HealthRatio = healthRatio,
            HasTarget = hasTarget,
            TargetDirection = targetDirection,
            TargetDistance = targetDistance,
            IsCasting = isCasting,
            RecentlyAttacked = combatState.PostAttackTimer > 0f
        };
        return true;
    }

    private static bool TryFindNearestEnemy(World.World world, World.Entity caster, float range, out World.Entity nearest) {
        nearest = null!;
        var bestDistSq = range * range;
        var found = false;

        foreach (var (entity, collider) in world.Query<CollisionComponent>()) {
            if (entity == caster || collider.Layer != CollisionLayer.Player) {
                continue;
            }

            var distSq = Vector3.DistanceSquared(caster.Position, entity.Position);
            if (distSq > bestDistSq) {
                continue;
            }

            bestDistSq = distSq;
            nearest = entity;
            found = true;
        }

        return found;
    }
}