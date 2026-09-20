using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Compatibility;

namespace Yggdrasilnet.Server.Simulation.World.System.Ai;

public sealed class AiSensorSystem : ISystem {
    public void Update(World world, float deltaTime) {
        foreach (var (entity, ai) in world.Query<AiComponent>()) {
            ai.PerceptionTimer -= deltaTime;
            if (ai.PerceptionTimer > 0f) {
                continue;
            }
            ai.PerceptionTimer = ai.PerceptionInterval;

            Scan(world, entity, ai);
        }
    }

    private static void Scan(World world, Entity entity, AiComponent ai) {
        var hasTarget = TryFindNearestEnemy(world, entity, ai.SightRange, out var target);

        if (!hasTarget) {
            ai.TargetEntityId = null;
            ai.TargetDistance = 0f;
            ai.TargetDirection = Vector3.Zero;
            ai.TargetHealthRatio = 0f;
        } else {
            var toTarget = target.Position - entity.Position;
            toTarget.Y = 0;

            var distance = toTarget.Length();
            var direction = distance > 0.0001f ? Vector3.Normalize(toTarget) : Vector3.Zero;

            var healthRatio = 0f;
            if (target.TryGetComponent<HealthComponent>(out var targetHealth) && targetHealth.Max > 0f) {
                healthRatio = targetHealth.Current / targetHealth.Max;
            }

            ai.TargetEntityId = target.Id;
            ai.TargetDistance = distance;
            ai.TargetDirection = direction;
            ai.TargetHealthRatio = healthRatio;
        }

        ai.AlliesNearby = CountAllies(world, entity, ai.SightRange);
        ai.DangerLevel = 0f;
    }

    private static bool TryFindNearestEnemy(World world, Entity caster, float range, out Entity nearest) {
        nearest = null!;
        var bestDistSq = range * range;
        var found = false;

        foreach (var (entity, collider) in world.Query<CollisionComponent>()) {
            if (entity == caster || collider.Layer != CollisionLayer.Monster) {
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

    private static int CountAllies(World world, Entity self, float range) {
        if (!self.TryGetComponent<CollisionComponent>(out var selfCollider)) {
            return 0;
        }

        var rangeSq = range * range;
        var count = 0;

        foreach (var (entity, collider) in world.Query<CollisionComponent>()) {
            if (entity == self || collider.Layer != selfCollider.Layer) {
                continue;
            }

            var distSq = Vector3.DistanceSquared(self.Position, entity.Position);
            if (distSq <= rangeSq) {
                count++;
            }
        }

        return count;
    }
}