using System.Numerics;

namespace Yggdrasilnet.Server.Simulation.World.System.Steering;

internal sealed class SteeringContext {
    private readonly List<SteeringAgent> _steeredAgents = new();
    private readonly List<PlayerTarget> _playerTargets = new();
    private readonly Dictionary<long, List<SteeringAgent>> _steeringGrid = new();
    private readonly Dictionary<long, List<PlayerTarget>> _playerGrid = new();
    private readonly Stack<List<SteeringAgent>> _steeringBuckets = new();
    private readonly Stack<List<PlayerTarget>> _playerBuckets = new();
    private int _playerMinCellX;
    private int _playerMaxCellX;
    private int _playerMinCellY;
    private int _playerMaxCellY;

    public long Tick { get; set; }
    public float DeltaTime { get; set; }
    public float CellSize { get; set; }

    public bool EnableWanderAndAvoid { get; set; }

    public bool HasDirection { get; set; }
    public Vector2 Direction { get; set; }
    public float Distance { get; set; }

    public List<SteeringAgent> SteeredAgents => _steeredAgents;
    public List<PlayerTarget> PlayerTargets => _playerTargets;

    public void ResetForTick() {
        _steeredAgents.Clear();
        _playerTargets.Clear();
        RecycleGrid(_steeringGrid, _steeringBuckets);
        RecycleGrid(_playerGrid, _playerBuckets);
    }

    public void FinishGridBuild() {
        _steeringBuckets.Clear();
        _playerBuckets.Clear();
    }

    public void BuildSteeringGrid(float cellSize) {
        for (var i = 0; i < _steeredAgents.Count; i++) {
            var agent = _steeredAgents[i];
            var key = GetCellKey(agent.Position, cellSize);
            if (!_steeringGrid.TryGetValue(key, out var bucket)) {
                bucket = _steeringBuckets.TryPop(out var recycled) ? recycled : new List<SteeringAgent>();
                _steeringGrid[key] = bucket;
            }

            bucket.Add(agent);
        }
    }

    public void BuildPlayerGrid(float cellSize) {
        var hasBounds = false;
        var minX = 0;
        var maxX = 0;
        var minY = 0;
        var maxY = 0;
        for (var i = 0; i < _playerTargets.Count; i++) {
            var target = _playerTargets[i];
            var (x, y) = GetCellCoordinates(target.Position, cellSize);
            if (!hasBounds) {
                minX = x;
                maxX = x;
                minY = y;
                maxY = y;
                hasBounds = true;
            } else {
                if (x < minX) {
                    minX = x;
                }

                if (x > maxX) {
                    maxX = x;
                }

                if (y < minY) {
                    minY = y;
                }

                if (y > maxY) {
                    maxY = y;
                }
            }

            var key = GetCellKey(x, y);
            if (!_playerGrid.TryGetValue(key, out var bucket)) {
                bucket = _playerBuckets.TryPop(out var recycled) ? recycled : new List<PlayerTarget>();
                _playerGrid[key] = bucket;
            }

            bucket.Add(target);
        }

        _playerMinCellX = minX;
        _playerMaxCellX = maxX;
        _playerMinCellY = minY;
        _playerMaxCellY = maxY;
    }

    public bool TryFindNearestTarget(Vector2 entityPosition, out PlayerTarget nearest, out float nearestDistanceSquared) {
        nearest = default;
        nearestDistanceSquared = float.MaxValue;
        var foundAny = false;

        var (centerX, centerY) = GetCellCoordinates(entityPosition, SteeringConstants.PlayerGridCellSize);
        var maxRing = ResolveMaxPlayerRing(centerX, centerY);
        for (var ring = 0; ring <= maxRing; ring++) {
            var width = 2L * ring + 1;
            if (width * width > _playerTargets.Count) {
                FindNearestTargetLinear(entityPosition, centerX, centerY, ref nearest, ref nearestDistanceSquared, ref foundAny);
                return foundAny;
            }

            if (ring == 0) {
                TryProcessPlayerCell(centerX, centerY, entityPosition, ref nearest, ref nearestDistanceSquared, ref foundAny);
            } else {
                var minX = centerX - ring;
                var maxX = centerX + ring;
                var minY = centerY - ring;
                var maxY = centerY + ring;

                for (var x = minX; x <= maxX; x++) {
                    TryProcessPlayerCell(x, minY, entityPosition, ref nearest, ref nearestDistanceSquared, ref foundAny);
                    TryProcessPlayerCell(x, maxY, entityPosition, ref nearest, ref nearestDistanceSquared, ref foundAny);
                }

                for (var y = minY + 1; y < maxY; y++) {
                    TryProcessPlayerCell(minX, y, entityPosition, ref nearest, ref nearestDistanceSquared, ref foundAny);
                    TryProcessPlayerCell(maxX, y, entityPosition, ref nearest, ref nearestDistanceSquared, ref foundAny);
                }
            }

            if (!foundAny) {
                continue;
            }

            var minXWorld = (centerX - ring) * SteeringConstants.PlayerGridCellSize;
            var maxXWorld = (centerX + ring + 1) * SteeringConstants.PlayerGridCellSize;
            var minYWorld = (centerY - ring) * SteeringConstants.PlayerGridCellSize;
            var maxYWorld = (centerY + ring + 1) * SteeringConstants.PlayerGridCellSize;

            var distanceToOutside = MathF.Min(
                MathF.Min(entityPosition.X - minXWorld, maxXWorld - entityPosition.X),
                MathF.Min(entityPosition.Y - minYWorld, maxYWorld - entityPosition.Y)
            );
            if (nearestDistanceSquared <= distanceToOutside * distanceToOutside) {
                break;
            }
        }

        return foundAny;
    }

    public void ApplyNeighborAvoidance(SteeringAgent agent, ref Vector2 steer) {
        var (centerX, centerY) = GetCellCoordinates(agent.Position, CellSize);
        var cellRadius = (int)MathF.Ceiling(agent.Steering.AvoidRadius / CellSize);
        var avoidRadiusSquared = agent.Steering.AvoidRadius * agent.Steering.AvoidRadius;
        var processedNeighbors = 0;
        for (var y = centerY - cellRadius; y <= centerY + cellRadius; y++) {
            for (var x = centerX - cellRadius; x <= centerX + cellRadius; x++) {
                if (!_steeringGrid.TryGetValue(GetCellKey(x, y), out var bucket)) {
                    continue;
                }

                foreach (var other in bucket) {
                    if (other.Entity.Id == agent.Entity.Id) {
                        continue;
                    }

                    processedNeighbors++;
                    if (processedNeighbors > SteeringConstants.MaxNeighborsPerEntity) {
                        return;
                    }

                    var away = agent.Position - other.Position;
                    var distanceSquared = away.LengthSquared();
                    if (!(distanceSquared > SteeringConstants.MinDistanceSquared) ||
                        !(distanceSquared < avoidRadiusSquared)) {
                        continue;
                    }

                    var inverseDistance = 1f / MathF.Sqrt(distanceSquared);
                    var distance = distanceSquared * inverseDistance;
                    var strength = (agent.Steering.AvoidRadius - distance) / agent.Steering.AvoidRadius;
                    steer += away * inverseDistance * strength * agent.Steering.AvoidWeight;
                }
            }
        }
    }

    private void FindNearestTargetLinear(
        Vector2 entityPosition,
        int centerX,
        int centerY,
        ref PlayerTarget nearest,
        ref float nearestDistanceSquared,
        ref bool foundAny
    ) {
        foreach (var target in _playerTargets) {
            var distanceSquared = (target.Position - entityPosition).LengthSquared();
            if (!(distanceSquared < nearestDistanceSquared) &&
                (!foundAny || !(Math.Abs(distanceSquared - nearestDistanceSquared) < 0.001f) ||
                 PlayerCellVisitOrder(target.Position, centerX, centerY)
                     .CompareTo(PlayerCellVisitOrder(nearest.Position, centerX, centerY)) >= 0)) {
                continue;
            }

            nearest = target;
            nearestDistanceSquared = distanceSquared;
            foundAny = true;
        }
    }

    private static (long Ring, long Order) PlayerCellVisitOrder(Vector2 position, int centerX, int centerY) {
        var (x, y) = GetCellCoordinates(position, SteeringConstants.PlayerGridCellSize);
        var dx = (long)x - centerX;
        var dy = (long)y - centerY;
        var ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (ring == 0) {
            return (0, 0);
        }

        var order = Math.Abs(dy) == ring
            ? (dx + ring) * 2 + (dy == -ring ? 0 : 1)
            : (2 * ring + 1) * 2 + (dy + ring - 1) * 2 + (dx == -ring ? 0 : 1);
        return (ring, order);
    }

    private void TryProcessPlayerCell(
        int cellX,
        int cellY,
        Vector2 entityPosition,
        ref PlayerTarget nearest,
        ref float nearestDistanceSquared,
        ref bool foundAny
    ) {
        if (!_playerGrid.TryGetValue(GetCellKey(cellX, cellY), out var bucket)) {
            return;
        }

        for (var i = 0; i < bucket.Count; i++) {
            var target = bucket[i];
            var distanceSquared = (target.Position - entityPosition).LengthSquared();
            if (distanceSquared < nearestDistanceSquared) {
                nearestDistanceSquared = distanceSquared;
                nearest = target;
                foundAny = true;
            }
        }
    }

    private int ResolveMaxPlayerRing(int centerX, int centerY) {
        return Math.Max(
            Math.Max(Math.Abs(centerX - _playerMinCellX), Math.Abs(centerX - _playerMaxCellX)),
            Math.Max(Math.Abs(centerY - _playerMinCellY), Math.Abs(centerY - _playerMaxCellY))
        );
    }

    private static void RecycleGrid<T>(Dictionary<long, List<T>> grid, Stack<List<T>> buckets) {
        foreach (var bucket in grid.Values) {
            bucket.Clear();
            buckets.Push(bucket);
        }
        grid.Clear();
    }

    private static (int X, int Y) GetCellCoordinates(Vector2 position, float cellSize) {
        var x = (int)MathF.Floor(position.X / cellSize);
        var y = (int)MathF.Floor(position.Y / cellSize);
        return (x, y);
    }

    private static long GetCellKey(Vector2 position, float cellSize) {
        var (x, y) = GetCellCoordinates(position, cellSize);
        return GetCellKey(x, y);
    }

    private static long GetCellKey(int x, int y) {
        return ((long)x << 32) | (uint)y;
    }
}
