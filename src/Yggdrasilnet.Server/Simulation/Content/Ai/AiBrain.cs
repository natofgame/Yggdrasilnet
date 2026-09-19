using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Compatibility;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content.Ai;

public static class AiBrain {
    public static void Tick(World.World world, World.Entity entity, float dt) {
        if (!entity.TryGetComponent<AiComponent>(out var ai)) {
            return;
        }
        if (!entity.TryGetComponent<HealthComponent>(out var hp) || hp.Max <= 0f) {
            return;
        }

        if (ai.AttackCooldown > 0f) {
            ai.AttackCooldown = MathF.Max(0f, ai.AttackCooldown - dt);
        }

        ai.HealthRatio = hp.Current / hp.Max;
        var hasTarget = TryFindNearestPlayer(
            world, entity, ai.SightRange,
            out var targetId, out var targetDistance, out var targetDirection, out var targetHealthRatio);

        ai.TargetEntityId = hasTarget ? targetId : null;
        ai.TargetDistance = hasTarget ? targetDistance : 0f;
        ai.TargetDirection = hasTarget ? targetDirection : Vector3.Zero;
        ai.TargetHealthRatio = hasTarget ? targetHealthRatio : 0f;

        if (hasTarget && !ai.HadTargetLastTick) {
            ai.IsSurprised = true;
            ai.SurprisedTimer = ai.SurprisedDuration;
        }
        ai.HadTargetLastTick = hasTarget;

        if (ai.SurprisedTimer > 0f) {
            ai.SurprisedTimer = MathF.Max(0f, ai.SurprisedTimer - dt);
            ai.IsSurprised = ai.SurprisedTimer > 0f;
            Stop(entity);
            return;
        }
        ai.IsSurprised = false;

        var isCasting = entity.TryGetComponent<ActionStateComponent>(out var action)
                        && action.Phase != SpellPhase.None;
        if (isCasting) {
            Stop(entity);
            return;
        }

        if (!entity.TryGetComponent<SteeringComponent>(out var steering)) {
            return;
        }

        Reset(steering);

        AiBehavior? best = null;
        var bestScore = 0f;
        foreach (var behavior in ai.Actions) {
            var score = behavior.Score(ai);
            if (score > bestScore) {
                bestScore = score;
                best = behavior;
            }
        }

        best?.Tick(entity, ai, steering, dt);
    }

    private static void Stop(World.Entity entity) {
        if (!entity.TryGetComponent<SteeringComponent>(out var steering)) {
            return;
        }
        Reset(steering);
        steering.MoveSpeed = 0f;
    }

    private static void Reset(SteeringComponent steering) {
        steering.InputDirection = Vector2.Zero;
        steering.SeekWeight = 0f;
        steering.CircleWeight = 0f;
        steering.HasDash = false;
    }

    private static bool TryFindNearestPlayer(
        World.World world,
        World.Entity self,
        float range,
        out int targetId,
        out float distance,
        out Vector3 direction,
        out float healthRatio
    ) {
        targetId = 0;
        distance = 0f;
        direction = Vector3.Zero;
        healthRatio = 0f;

        World.Entity? nearest = null;
        var bestDistSq = range * range;

        foreach (var (other, collider) in world.Query<CollisionComponent>()) {
            if (other == self || collider.Layer != CollisionLayer.Player) {
                continue;
            }

            var distSq = Vector3.DistanceSquared(self.Position, other.Position);
            if (distSq > bestDistSq) {
                continue;
            }

            bestDistSq = distSq;
            nearest = other;
        }

        if (nearest is null) {
            return false;
        }

        if (nearest.TryGetComponent<HealthComponent>(out var targetHp) && targetHp.Max > 0f) {
            healthRatio = targetHp.Current / targetHp.Max;
        }

        var toTarget = nearest.Position - self.Position;
        toTarget.Y = 0;
        distance = toTarget.Length();
        if (toTarget.LengthSquared() > 0.0001f) {
            direction = Vector3.Normalize(toTarget);
        }

        targetId = nearest.Id;
        return true;
    }
}
