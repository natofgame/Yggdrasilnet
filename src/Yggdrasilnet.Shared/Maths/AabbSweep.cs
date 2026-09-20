using System.Numerics;

namespace Yggdrasilnet.Shared.Maths;

public static class AabbSweep {
    private const float Epsilon = 1e-8f;

    public static bool TryGetTimeOfImpact(
        in BoundingBoxes moving,
        Vector3 displacement,
        in BoundingBoxes obstacle,
        out float timeOfImpact,
        out Vector3 normal
    ) {
        timeOfImpact = 0f;
        normal = Vector3.Zero;

        var min = obstacle.Min - moving.Max;
        var max = obstacle.Max - moving.Min;

        var overlapping = min.X <= 0f && max.X >= 0f
                          && min.Y <= 0f && max.Y >= 0f
                          && min.Z <= 0f && max.Z >= 0f;
        if (overlapping) {
            return true;
        }

        if (displacement.LengthSquared() <= Epsilon) {
            return false;
        }

        var tEnter = 0f;
        var tExit = 1f;
        if (!ClipAxis(displacement.X, min.X, max.X, Vector3.UnitX, ref tEnter, ref tExit, ref normal)
            || !ClipAxis(displacement.Y, min.Y, max.Y, Vector3.UnitY, ref tEnter, ref tExit, ref normal)
            || !ClipAxis(displacement.Z, min.Z, max.Z, Vector3.UnitZ, ref tEnter, ref tExit, ref normal)) {
            return false;
        }

        if (tEnter > tExit || tEnter > 1f || tExit < 0f) {
            return false;
        }

        timeOfImpact = tEnter;
        return tEnter > 0f;
    }

// AabbSweep
    public static bool TryComputeMinimumTranslation(in BoundingBoxes a, in BoundingBoxes b, out Vector3 t)
    {
        t = Vector3.Zero;
        if (!a.Intersects(b)) return false;

        var pushX = (b.Max.X - a.Min.X) < (a.Max.X - b.Min.X) ? (b.Max.X - a.Min.X) : -(a.Max.X - b.Min.X);
        var pushZ = (b.Max.Z - a.Min.Z) < (a.Max.Z - b.Min.Z) ? (b.Max.Z - a.Min.Z) : -(a.Max.Z - b.Min.Z);

        t = MathF.Abs(pushX) <= MathF.Abs(pushZ)
            ? new Vector3(pushX, 0f, 0f)
            : new Vector3(0f, 0f, pushZ);
        return true;
    }

    private static bool ClipAxis(
        float direction,
        float min,
        float max,
        Vector3 axis,
        ref float tEnter,
        ref float tExit,
        ref Vector3 normal
    ) {
        if (MathF.Abs(direction) <= Epsilon) {
            return max >= 0f && min <= 0f;
        }

        var inverse = 1f / direction;
        var t1 = min * inverse;
        var t2 = max * inverse;
        var enterNormal = direction > 0f ? -axis : axis;
        if (t1 > t2) {
            (t1, t2) = (t2, t1);
            enterNormal = -enterNormal;
        }

        if (t1 > tEnter) {
            tEnter = t1;
            normal = enterNormal;
        }

        if (t2 < tExit) {
            tExit = t2;
        }

        return tEnter <= tExit;
    }
}
