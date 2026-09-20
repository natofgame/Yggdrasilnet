using System.Numerics;
using Yggdrasilnet.Gameplay.Enums;
using Yggdrasilnet.Network.Enums;
using Yggdrasilnet.Server.Simulation.World.Component;

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
        var hasTarget = RefreshTarget(world, entity, ai, dt);

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

        best?.Tick(world, entity, ai, steering, dt);
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

    private static bool RefreshTarget(World.World world, World.Entity self, AiComponent ai, float dt) {
        if (TryKeepCurrentTarget(world, self, ai)) {
            if (ai.PerceptionTimer > 0f) {
                ai.PerceptionTimer -= dt;
            }
            if (ai.PerceptionTimer <= 0f) {
                ai.PerceptionTimer = ai.PerceptionInterval;
            }
            return true;
        }

        ai.PerceptionTimer -= dt;
        if (ai.TargetEntityId is not null || ai.PerceptionTimer <= 0f) {
            ai.PerceptionTimer = ai.PerceptionInterval;
            if (TryFindNearestPlayer(world, self, ai.SightRange, out var target)) {
                ApplyTarget(self, ai, target);
                return true;
            }
        }

        ClearTarget(ai);
        return false;
    }

    private static bool TryKeepCurrentTarget(World.World world, World.Entity self, AiComponent ai) {
        if (ai.TargetEntityId is not { } targetId || !world.TryGetEntity(targetId, out var current)) {
            return false;
        }

        if (!current.TryGetComponent<CollisionComponent>(out var collider)
            || collider.Layer != CollisionLayer.Player) {
            return false;
        }

        if (current.TryGetComponent<HealthComponent>(out var hp) && hp.Current <= 0f) {
            return false;
        }

        ApplyTarget(self, ai, current);
        return ai.TargetDistance <= ai.SightRange;
    }

    private static bool TryFindNearestPlayer(World.World world, World.Entity self, float range, out World.Entity nearest) {
        nearest = null!;
        var bestDistSq = range * range;
        var found = false;

        foreach (var (other, collider) in world.Query<CollisionComponent>()) {
            if (other == self || collider.Layer != CollisionLayer.Player) {
                continue;
            }

            if (other.TryGetComponent<HealthComponent>(out var hp) && hp.Current <= 0f) {
                continue;
            }

            var distSq = Vector3.DistanceSquared(self.Position, other.Position);
            if (distSq > bestDistSq) {
                continue;
            }

            bestDistSq = distSq;
            nearest = other;
            found = true;
        }

        return found;
    }

    private static void ApplyTarget(World.Entity self, AiComponent ai, World.Entity target) {
        ai.TargetEntityId = target.Id;
        ai.TargetHealthRatio = 0f;
        if (target.TryGetComponent<HealthComponent>(out var targetHp) && targetHp.Max > 0f) {
            ai.TargetHealthRatio = targetHp.Current / targetHp.Max;
        }

        var toTarget = target.Position - self.Position;
        toTarget.Y = 0;
        ai.TargetDistance = toTarget.Length();
        ai.TargetDirection = toTarget.LengthSquared() > 0.0001f
            ? Vector3.Normalize(toTarget)
            : Vector3.Zero;
    }

    private static void ClearTarget(AiComponent ai) {
        ai.TargetEntityId = null;
        ai.TargetDistance = 0f;
        ai.TargetDirection = Vector3.Zero;
        ai.TargetHealthRatio = 0f;
    }
}
