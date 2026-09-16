using System.Numerics;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Compatibility;

namespace Yggdrasilnet.Server.Simulation.Content.Spell;

public static class SpellCastValidator {
    private const float MinVelocitySq = 0.0001f;

    public static bool IsLiving(World.Entity entity) =>
        entity.TryGetComponent<HealthComponent>(out var health) && float.IsFinite(health.Current) && health.Current > 0f;

    public static bool TryCreate(World.World world, World.Entity caster, SpellDefinition spell, out SpellCastContext context) {
        context = null!;
        if (!IsLiving(caster) || !Enum.IsDefined(spell.Type)) {
            return false;
        }

        var direction = ResolveDirection(world, caster, spell.Targeting.Range);
        context = new SpellCastContext(caster.Id, spell.Id, spell.Type, direction, caster.Position);
        return true;
    }

    private static Vector3 ResolveDirection(World.World world, World.Entity caster, float range) {
        if (TryFindNearestEnemy(world, caster, range, out var nearest)) {
            var toTarget = nearest.Position - caster.Position with { Y = 0 };
            if (toTarget.LengthSquared() > 0.0001f) {
                return Vector3.Normalize(toTarget with { Y = 0 });
            }
        }

        if (caster.TryGetComponent<VelocityComponent>(out var velocity)) {
            var flat = new Vector3(velocity.X, 0, velocity.Z);
            if (flat.LengthSquared() > MinVelocitySq) {
                return Vector3.Normalize(flat);
            }
        }

        return Vector3.Zero;
    }

    private static bool TryFindNearestEnemy(World.World world, World.Entity caster, float range, out World.Entity nearest) {
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
}