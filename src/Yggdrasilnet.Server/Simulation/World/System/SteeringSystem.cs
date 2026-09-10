using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System;

public sealed class SteeringSystem : ISystem {
    private const int MaxNeighborsPerEntity = 10;
    private const float NearPlayerDistance = 18f;
    private const float MidPlayerDistance = 36f;
    private const float WanderAvoidDistance = 28f;
    private const float PlayerGridCellSize = MidPlayerDistance;
    private const float MinDistanceSquared = 0.0001f;

    private const float NearPlayerDistanceSquared = NearPlayerDistance * NearPlayerDistance;
    private const float MidPlayerDistanceSquared = MidPlayerDistance * MidPlayerDistance;
    private const float WanderAvoidDistanceSquared = WanderAvoidDistance * WanderAvoidDistance;

    private long _steeringTick;
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

    public void Update(World world, float deltaTime) {
        _steeringTick++;

        _steeredAgents.Clear();
        _playerTargets.Clear();
        RecycleGrid(_steeringGrid, _steeringBuckets);
        RecycleGrid(_playerGrid, _playerBuckets);
        var maxAvoidRadius = 0f;
        var hasAvoidance = false;
        foreach (var (entity, steering) in world.Query<SteeringComponent>()) {
            if (!entity.TryGetComponent<VelocityComponent>(out var velocity)) {
                continue;
            }

            _steeredAgents.Add(new SteeringAgent(entity, steering, velocity, Flat(entity.Position)));
            hasAvoidance |= HasAvoidance(steering);
            if (steering.AvoidRadius > maxAvoidRadius) {
                maxAvoidRadius = steering.AvoidRadius;
            }
        }

        if (_steeredAgents.Count == 0) {
            _steeringBuckets.Clear();
            _playerBuckets.Clear();
            return;
        }

        // Keep the original cell size even for disabled neighbors: it determines
        // traversal order and therefore which ten neighbors contribute.
        var cellSize = MathF.Max(0.1f, maxAvoidRadius);
        if (hasAvoidance) {
            BuildSteeringGrid(_steeredAgents, cellSize);
        }
        _steeringBuckets.Clear();

        foreach (var (entity, _) in world.Query<InputComponent>()) {
            _playerTargets.Add(new PlayerTarget(entity, Flat(entity.Position)));
        }

        if (_playerTargets.Count == 0) {
            _playerBuckets.Clear();
            ApplyNoPlayerSteering(cellSize, deltaTime);
            return;
        }

        BuildPlayerGrid(_playerTargets, PlayerGridCellSize);
        _playerBuckets.Clear();

        for (var i = 0; i < _steeredAgents.Count; i++) {
            var agent = _steeredAgents[i];
            if (!TryFindNearestTarget(agent.Position, out var target, out var nearestPlayerDistanceSquared)) {
                continue;
            }

            var updateInterval = ResolveUpdateInterval(nearestPlayerDistanceSquared);
            if (updateInterval > 1 && ((_steeringTick + agent.Entity.Id) % updateInterval) != 0) {
                continue;
            }

            var enableWanderAndAvoid = nearestPlayerDistanceSquared <= WanderAvoidDistanceSquared;
            var steer = Vector2.Zero;

            {
                var toTarget = target.Position - agent.Position;
                var distanceSquared = toTarget.LengthSquared();
                if (distanceSquared > MinDistanceSquared) {
                    var inverseDistance = 1f / MathF.Sqrt(distanceSquared);
                    var direction = toTarget * inverseDistance;
                    var distance = distanceSquared * inverseDistance;

                    steer += direction * agent.Steering.SeekWeight;

                    var radialError = distance - agent.Steering.CircleRadius;
                    var radialStrength = MathF.Min(MathF.Abs(radialError), 1f) * MathF.Sign(radialError);
                    steer += direction * radialStrength * agent.Steering.CircleWeight;

                    var tangent = new Vector2(-direction.Y, direction.X) * agent.Steering.CircleDirection;
                    steer += tangent * agent.Steering.CircleWeight;

                    if (enableWanderAndAvoid && distance < agent.Steering.AvoidRadius) {
                        var strength = (agent.Steering.AvoidRadius - distance) / agent.Steering.AvoidRadius;
                        steer -= direction * strength * agent.Steering.AvoidWeight;
                    }
                }
            }

            if (enableWanderAndAvoid) {
                ApplyNeighborAvoidance(agent, cellSize, ref steer);
            }
            ApplyWander(agent, deltaTime, ref steer);

            ApplySpawnRoamConstraint(agent, ref steer);

            ApplySteeringVelocity(agent, steer);
        }
    }

    private bool TryFindNearestTarget(Vector2 entityPosition, out PlayerTarget nearest, out float nearestDistanceSquared) {
        nearest = default;
        nearestDistanceSquared = float.MaxValue;
        var foundAny = false;

        var (centerX, centerY) = GetCellCoordinates(entityPosition, PlayerGridCellSize);
        var maxRing = ResolveMaxPlayerRing(centerX, centerY);
        for (var ring = 0; ring <= maxRing; ring++) {
            var width = 2L * ring + 1;
            if (width * width > _playerTargets.Count) {
                // Sparse or distant targets cost less to scan than empty cell rings.
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

            if (foundAny) {
                var minXWorld = (centerX - ring) * PlayerGridCellSize;
                var maxXWorld = (centerX + ring + 1) * PlayerGridCellSize;
                var minYWorld = (centerY - ring) * PlayerGridCellSize;
                var maxYWorld = (centerY + ring + 1) * PlayerGridCellSize;

                var distanceToOutside = MathF.Min(
                    MathF.Min(entityPosition.X - minXWorld, maxXWorld - entityPosition.X),
                    MathF.Min(entityPosition.Y - minYWorld, maxYWorld - entityPosition.Y)
                );
                if (nearestDistanceSquared <= distanceToOutside * distanceToOutside) {
                    break;
                }
            }
        }

        return foundAny;
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
            if (distanceSquared < nearestDistanceSquared ||
                (foundAny && distanceSquared == nearestDistanceSquared &&
                 PlayerCellVisitOrder(target.Position, centerX, centerY)
                     .CompareTo(PlayerCellVisitOrder(nearest.Position, centerX, centerY)) < 0)) {
                nearest = target;
                nearestDistanceSquared = distanceSquared;
                foundAny = true;
            }
        }
    }

    private static (long Ring, long Order) PlayerCellVisitOrder(Vector2 position, int centerX, int centerY) {
        var (x, y) = GetCellCoordinates(position, PlayerGridCellSize);
        var dx = (long)x - centerX;
        var dy = (long)y - centerY;
        var ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (ring == 0) {
            return (0, 0);
        }

        // Match the ring scan's horizontal edges, then vertical edges. Equal
        // orders leave the first target in the bucket selected.
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

    private static int ResolveUpdateInterval(float nearestPlayerDistanceSquared) {
        if (nearestPlayerDistanceSquared <= NearPlayerDistanceSquared) {
            return 1;
        }

        if (nearestPlayerDistanceSquared <= MidPlayerDistanceSquared) {
            return 2;
        }

        return 3;
    }

    private void ApplyNoPlayerSteering(float cellSize, float deltaTime) {
        for (var i = 0; i < _steeredAgents.Count; i++) {
            var agent = _steeredAgents[i];
            var steer = Vector2.Zero;
            ApplyNeighborAvoidance(agent, cellSize, ref steer);
            ApplyWander(agent, deltaTime, ref steer);
            ApplySpawnRoamConstraint(agent, ref steer);

            ApplySteeringVelocity(agent, steer);
        }
    }

    private static void ApplySteeringVelocity(SteeringAgent agent, Vector2 steer) {
        var steerLengthSquared = steer.LengthSquared();
        if (steerLengthSquared <= MinDistanceSquared) {
            agent.Velocity.X = 0f;
            agent.Velocity.Z = 0f;
            return;
        }

        var steerLength = MathF.Sqrt(steerLengthSquared);
        var direction = steer / steerLength;
        var speedFactor = MathF.Min(1f, steerLength);
        var speed = agent.Steering.MoveSpeed * speedFactor;

        agent.Velocity.X = direction.X * speed;
        agent.Velocity.Z = direction.Y * speed;
    }

    private static void ApplySpawnRoamConstraint(SteeringAgent agent, ref Vector2 steer) {
        var roamRadius = agent.Steering.RoamRadius;
        if (roamRadius <= 0f) {
            return;
        }

        var spawn = new Vector2(agent.Steering.SpawnX, agent.Steering.SpawnZ);
        var fromSpawn = agent.Position - spawn;
        var distanceSquared = fromSpawn.LengthSquared();
        if (distanceSquared <= MinDistanceSquared) {
            return;
        }

        var distance = MathF.Sqrt(distanceSquared);
        var outward = fromSpawn / distance;
        var radial = Vector2.Dot(steer, outward);
        var inward = -outward;
        var edgeBand = MathF.Max(0.5f, roamRadius * 0.2f);
        var bandStart = MathF.Max(0f, roamRadius - edgeBand);

        if (distance < bandStart) {
            return;
        }

        if (distance >= roamRadius) {
            if (radial > 0f) {
                steer -= outward * radial;
            }

            var overflow = distance - roamRadius;
            var pullStrength = 1f + MathF.Min(1f, overflow / edgeBand);
            steer += inward * pullStrength;
            return;
        }

        var edgeFactor = (distance - bandStart) / MathF.Max(0.001f, roamRadius - bandStart);
        if (radial > 0f) {
            steer -= outward * radial * edgeFactor;
        }

        steer += inward * edgeFactor * 0.35f;
    }

    private static void ApplyWander(SteeringAgent agent, float deltaTime, ref Vector2 steer) {
        if (agent.Steering.WanderWeight <= 0f) {
            return;
        }

        var jitter = ((float)Random.Shared.NextDouble() * 2f - 1f) * agent.Steering.WanderJitter * deltaTime;
        agent.Steering.WanderAngle += jitter;
        var wanderDirection = new Vector2(MathF.Cos(agent.Steering.WanderAngle), MathF.Sin(agent.Steering.WanderAngle));
        steer += wanderDirection * agent.Steering.WanderWeight;
    }

    private void BuildPlayerGrid(List<PlayerTarget> targets, float cellSize) {
        var hasBounds = false;
        var minX = 0;
        var maxX = 0;
        var minY = 0;
        var maxY = 0;
        for (var i = 0; i < targets.Count; i++) {
            var target = targets[i];
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

    private void ApplyNeighborAvoidance(SteeringAgent agent, float cellSize, ref Vector2 steer) {
        if (!HasAvoidance(agent.Steering)) {
            return;
        }

        var (centerX, centerY) = GetCellCoordinates(agent.Position, cellSize);
        var cellRadius = (int)MathF.Ceiling(agent.Steering.AvoidRadius / cellSize);
        var avoidRadiusSquared = agent.Steering.AvoidRadius * agent.Steering.AvoidRadius;
        var processedNeighbors = 0;
        for (var y = centerY - cellRadius; y <= centerY + cellRadius; y++) {
            for (var x = centerX - cellRadius; x <= centerX + cellRadius; x++) {
                if (!_steeringGrid.TryGetValue(GetCellKey(x, y), out var bucket)) {
                    continue;
                }

                for (var i = 0; i < bucket.Count; i++) {
                    var other = bucket[i];
                    if (other.Entity.Id == agent.Entity.Id) {
                        continue;
                    }

                    processedNeighbors++;
                    if (processedNeighbors > MaxNeighborsPerEntity) {
                        return;
                    }

                    var away = agent.Position - other.Position;
                    var distanceSquared = away.LengthSquared();
                    if (distanceSquared > MinDistanceSquared && distanceSquared < avoidRadiusSquared) {
                        var inverseDistance = 1f / MathF.Sqrt(distanceSquared);
                        var distance = distanceSquared * inverseDistance;
                        var strength = (agent.Steering.AvoidRadius - distance) / agent.Steering.AvoidRadius;
                        steer += away * inverseDistance * strength * agent.Steering.AvoidWeight;
                    }
                }
            }
        }
    }

    private void BuildSteeringGrid(List<SteeringAgent> steered, float cellSize) {
        for (var i = 0; i < steered.Count; i++) {
            var agent = steered[i];
            var key = GetCellKey(agent.Position, cellSize);
            if (!_steeringGrid.TryGetValue(key, out var bucket)) {
                bucket = _steeringBuckets.TryPop(out var recycled) ? recycled : new List<SteeringAgent>();
                _steeringGrid[key] = bucket;
            }

            bucket.Add(agent);
        }
    }

    private static bool HasAvoidance(SteeringComponent steering) =>
        steering.AvoidRadius > 0f && steering.AvoidWeight != 0f;

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

    private static Vector2 Flat(Vector3 position) => new(position.X, position.Z);

    private readonly record struct SteeringAgent(Entity Entity, SteeringComponent Steering, VelocityComponent Velocity, Vector2 Position);

    private readonly record struct PlayerTarget(Entity Entity, Vector2 Position);
}
