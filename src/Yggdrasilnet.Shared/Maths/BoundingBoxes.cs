using System.Numerics;

namespace Yggdrasilnet.Shared.Maths
{
    public struct BoundingBoxes
    {
        public Vector3 Min;
        public Vector3 Max;

        public BoundingBoxes(Vector3 min, Vector3 max) {
            Min = min;
            Max = max;
        }

        public Vector3 Center => new(
            (Min.X + Max.X) / 2,
            (Min.Y + Max.Y) / 2,
            (Min.Z + Max.Z) / 2
        );
        
        public Vector3 Extents => new (
            (Max.X - Min.X) / 2,
            (Max.Y - Min.Y) / 2,
            (Max.Z - Min.Z) / 2
        );

        public bool Intersects(in BoundingBoxes other) {
            return Min.X <= other.Max.X && Max.X >= other.Min.X
                                        && Min.Y <= other.Max.Y && Max.Y >= other.Min.Y
                                        && Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
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
        
        public static BoundingBoxes From(Vector3 center, Vector3 size)
        {
            var half = new Vector3(size.X * 0.5f, size.Y * 0.5f, size.Z * 0.5f);
            return new BoundingBoxes(
                new Vector3(center.X - half.X, center.Y - half.Y, center.Z - half.Z),
                new Vector3(center.X + half.X, center.Y + half.Y, center.Z + half.Z)
            );
        }
    }
}