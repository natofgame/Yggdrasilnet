using System.Numerics;
using Yggdrasilnet.Maths.Collision;
using Yggdrasilnet.Network.Enums;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Spell;

public static class SpellTargetResolver {
    public static IEnumerable<World.Entity> Resolve(World.World world, SpellCastContext context, SpellTargeting targeting, World.Entity caster) {
        var box = ResolveHitVolume(context, targeting, caster);
        var enemyLayer = ResolveEnemyLayer(caster);
        return world.QueryBox(box, (entity, collider) => collider.Layer == enemyLayer && entity != caster);
    }

    public static BoundingBoxes ResolveHitVolume(SpellCastContext context, SpellTargeting targeting, World.Entity caster) {
        var (min, max) = ResolveHitBox(targeting, context.Direction);
        var volume = BoundingBoxes.FromOffsets(context.Origin, min, max);

        var range = MathF.Max(0f, targeting.Range);
        if (range > 0f && context.Direction.LengthSquared() > 0.0001f) {
            var facing = Vector3.Normalize(context.Direction with { Y = 0f });
            if (facing.LengthSquared() > 0.0001f) {
                volume = volume.ExpandAlong(facing, range);
            }
        }

        var casterDelta = caster.Position - caster.PreviousPosition;
        if (casterDelta.LengthSquared() > 0.0001f) {
            volume = volume.Sweep(casterDelta);
        }

        return volume;
    }

    private static (Vector3 Min, Vector3 Max) ResolveHitBox(SpellTargeting targeting, Vector3 direction) {
        if (targeting.CardinalHitBoxMin is not { } cardMin || targeting.CardinalHitBoxMax is not { } cardMax) {
            return (targeting.HitBoxMin, targeting.HitBoxMax);
        }

        var absX = MathF.Abs(direction.X);
        var absZ = MathF.Abs(direction.Z);
        var total = absX + absZ;
        var t = total > 0.0001f ? absZ / total : 0f;
        t = t * t * (3f - 2f * t);
        return (Vector3.Lerp(targeting.HitBoxMin, cardMin, t), Vector3.Lerp(targeting.HitBoxMax, cardMax, t));
    }

    public static CollisionLayer ResolveEnemyLayer(World.Entity caster) {
        if (caster.TryGetComponent<CollisionComponent>(out var collider) && collider.Layer == CollisionLayer.Player) {
            return CollisionLayer.Monster;
        }

        return CollisionLayer.Player;
    }

    public static float ResolveAimRadius(SpellTargeting targeting) {
        var min = targeting.HitBoxMin;
        var max = targeting.HitBoxMax;
        var extentX = MathF.Max(MathF.Abs(min.X), MathF.Abs(max.X));
        var extentZ = MathF.Max(MathF.Abs(min.Z), MathF.Abs(max.Z));
        if (targeting.CardinalHitBoxMin is { } cardMin && targeting.CardinalHitBoxMax is { } cardMax) {
            extentX = MathF.Max(extentX, MathF.Max(MathF.Abs(cardMin.X), MathF.Abs(cardMax.X)));
            extentZ = MathF.Max(extentZ, MathF.Max(MathF.Abs(cardMin.Z), MathF.Abs(cardMax.Z)));
        }

        return targeting.Range + MathF.Max(extentX, extentZ);
    }
}
