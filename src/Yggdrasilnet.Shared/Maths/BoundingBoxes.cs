using System.Numerics;

namespace Yggdrasilnet.Shared.Maths;

public struct BoundingBoxes {
    public Vector3 Min;
    public Vector3 Max;

    public BoundingBoxes(Vector3 min, Vector3 max) {
        Min = min;
        Max = max;
    }

    public Vector3 Center => new(
        (Min.X + Max.X) * 0.5f,
        (Min.Y + Max.Y) * 0.5f,
        (Min.Z + Max.Z) * 0.5f
    );

    public Vector3 Extents => new(
        (Max.X - Min.X) * 0.5f,
        (Max.Y - Min.Y) * 0.5f,
        (Max.Z - Min.Z) * 0.5f
    );

    public readonly bool Intersects(in BoundingBoxes other) {
        return Min.X <= other.Max.X && Max.X >= other.Min.X
                                    && Min.Y <= other.Max.Y && Max.Y >= other.Min.Y
                                    && Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
    }

    public BoundingBoxes Encapsulate(in BoundingBoxes other) {
        return new BoundingBoxes(
            new Vector3(
                MathF.Min(Min.X, other.Min.X),
                MathF.Min(Min.Y, other.Min.Y),
                MathF.Min(Min.Z, other.Min.Z)),
            new Vector3(
                MathF.Max(Max.X, other.Max.X),
                MathF.Max(Max.Y, other.Max.Y),
                MathF.Max(Max.Z, other.Max.Z)));
    }

    public BoundingBoxes Sweep(Vector3 delta) {
        return ExpandAlong(delta, 1f);
    }

    public BoundingBoxes ExpandAlong(Vector3 direction, float distance) {
        var offsetX = direction.X * distance;
        var offsetY = direction.Y * distance;
        var offsetZ = direction.Z * distance;
        return new BoundingBoxes(
            new Vector3(
                Min.X + MathF.Min(0f, offsetX),
                Min.Y + MathF.Min(0f, offsetY),
                Min.Z + MathF.Min(0f, offsetZ)),
            new Vector3(
                Max.X + MathF.Max(0f, offsetX),
                Max.Y + MathF.Max(0f, offsetY),
                Max.Z + MathF.Max(0f, offsetZ))
        );
    }

    public float DistanceSquaredTo(Vector3 point) {
        var dx = point.X < Min.X ? Min.X - point.X : point.X > Max.X ? point.X - Max.X : 0f;
        var dy = point.Y < Min.Y ? Min.Y - point.Y : point.Y > Max.Y ? point.Y - Max.Y : 0f;
        var dz = point.Z < Min.Z ? Min.Z - point.Z : point.Z > Max.Z ? point.Z - Max.Z : 0f;
        return dx * dx + dy * dy + dz * dz;
    }

    public static BoundingBoxes From(Vector3 center, Vector3 size) {
        var half = new Vector3(size.X * 0.5f, size.Y * 0.5f, size.Z * 0.5f);
        return new BoundingBoxes(
            new Vector3(center.X - half.X, center.Y - half.Y, center.Z - half.Z),
            new Vector3(center.X + half.X, center.Y + half.Y, center.Z + half.Z)
        );
    }

    public static BoundingBoxes FromOffsets(Vector3 origin, Vector3 minOffset, Vector3 maxOffset) {
        var a = origin + minOffset;
        var b = origin + maxOffset;
        return new BoundingBoxes(
            new Vector3(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Min(a.Z, b.Z)),
            new Vector3(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y), MathF.Max(a.Z, b.Z)));
    }
}
