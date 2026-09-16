using System.Numerics;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Shared.Compatibility;
using Yggdrasilnet.Shared.Maths;

namespace Yggdrasilnet.Server.Simulation.Content.Spell;

public static class SpellTargetResolver {
    // TODO: Create enemy custom definition
    public static IEnumerable<World.Entity> Resolve(World.World world, SpellCastContext context, SpellTargeting targeting, World.Entity caster) {
        var origin = context.Origin + context.Direction * targeting.Range;
        var (min, max) = ResolveHitBox(targeting, context.Direction);
        var box = new BoundingBoxes(origin + min, origin + max);
        return world.QueryBox(box, (e, c) => c.Layer == CollisionLayer.Monster && e != caster);
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
}