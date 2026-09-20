using Yggdrasilnet.Maths.Collision;

namespace Yggdrasilnet.Server.Simulation.World.System.Physic.Collision;

internal sealed class CollisionSpatialHash {
    private const float DefaultCellSize = 2f;
    private readonly Dictionary<long, List<int>> _cells = new();
    private readonly Stack<List<int>> _bucketPool = new();
    private readonly List<int> _queryBuffer = new();
    private int[] _stamps = [];
    private int _stamp;

    public float CellSize { get; private set; } = DefaultCellSize;

    public void Rebuild(IReadOnlyList<BoundingBoxes> boxes, float cellSize) {
        Recycle();
        CellSize = cellSize > 0.1f ? cellSize : DefaultCellSize;
        EnsureStamps(boxes.Count);

        for (var i = 0; i < boxes.Count; i++) {
            Insert(i, boxes[i]);
        }
    }

    public List<int> Query(in BoundingBoxes box) {
        _queryBuffer.Clear();
        _stamp++;
        if (_stamp == int.MaxValue) {
            Array.Clear(_stamps);
            _stamp = 1;
        }

        var minX = Floor(box.Min.X);
        var maxX = Floor(box.Max.X);
        var minZ = Floor(box.Min.Z);
        var maxZ = Floor(box.Max.Z);

        for (var z = minZ; z <= maxZ; z++) {
            for (var x = minX; x <= maxX; x++) {
                if (!_cells.TryGetValue(Pack(x, z), out var bucket)) {
                    continue;
                }

                for (var i = 0; i < bucket.Count; i++) {
                    var index = bucket[i];
                    if (_stamps[index] == _stamp) {
                        continue;
                    }

                    _stamps[index] = _stamp;
                    _queryBuffer.Add(index);
                }
            }
        }

        return _queryBuffer;
    }

    private void Insert(int index, in BoundingBoxes box) {
        var minX = Floor(box.Min.X);
        var maxX = Floor(box.Max.X);
        var minZ = Floor(box.Min.Z);
        var maxZ = Floor(box.Max.Z);

        for (var z = minZ; z <= maxZ; z++) {
            for (var x = minX; x <= maxX; x++) {
                var key = Pack(x, z);
                if (!_cells.TryGetValue(key, out var bucket)) {
                    bucket = _bucketPool.TryPop(out var recycled) ? recycled : new List<int>();
                    _cells[key] = bucket;
                }

                bucket.Add(index);
            }
        }
    }

    private void Recycle() {
        foreach (var bucket in _cells.Values) {
            bucket.Clear();
            _bucketPool.Push(bucket);
        }

        _cells.Clear();
    }

    private void EnsureStamps(int count) {
        if (_stamps.Length >= count) {
            return;
        }

        Array.Resize(ref _stamps, Math.Max(count, _stamps.Length * 2 + 8));
    }

    private int Floor(float value) => (int)MathF.Floor(value / CellSize);

    private static long Pack(int x, int z) => ((long)x << 32) | (uint)z;
}
