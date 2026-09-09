using System.Linq;
using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System;

public sealed class SteeringSystem : ISystem {
    private const int MaxNeighborsPerEntity = 10;
    private const float NearPlayerDistance = 18f;
    private const float MidPlayerDistance = 36f;
    private const float WanderAvoidDistance = 28f;
    private const float MinDistanceSquared = 0.0001f;

    private const float NearPlayerDistanceSquared = NearPlayerDistance * NearPlayerDistance;
    private const float MidPlayerDistanceSquared = MidPlayerDistance * MidPlayerDistance;
    private const float WanderAvoidDistanceSquared = WanderAvoidDistance * WanderAvoidDistance;

    private long _steeringTick;

    public void Update(World world, float deltaTime) {
        _steeringTick++;

        var steered = world.Query<SteeringComponent>().ToList();
        var targets = world.Query<InputComponent>().ToList();
        if (steered.Count == 0) {
            return;
        }

        var maxAvoidRadius = steered.Max(pair => pair.Component.AvoidRadius);
        var cellSize = MathF.Max(0.1f, maxAvoidRadius);
        var steeringGrid = BuildSteeringGrid(steered, cellSize);
        if (targets.Count == 0) {
            ApplyNoPlayerSteering(steered, steeringGrid, cellSize);
            return;
        }

        foreach (var (entity, steering) in steered) {
            if (!entity.TryGetComponent<VelocityComponent>(out var velocity)) {
                continue;
            }

            var selfPosition = Flat(entity.Position);
            var target = FindNearestTarget(selfPosition, targets, out var nearestPlayerDistanceSquared);
            var updateInterval = ResolveUpdateInterval(nearestPlayerDistanceSquared);
            if (updateInterval > 1 && ((_steeringTick + entity.Id) % updateInterval) != 0) {
                continue;
            }

            var enableWanderAndAvoid = nearestPlayerDistanceSquared <= WanderAvoidDistanceSquared;
            var steer = Vector2.Zero;

            if (target != null) {
                var toTarget = Flat(target.Position) - selfPosition;
                var distanceSquared = toTarget.LengthSquared();
                var distance = MathF.Sqrt(distanceSquared);

                if (distanceSquared > MinDistanceSquared) {
                    var direction = toTarget / distance;

                    steer += direction * steering.SeekWeight;

                    var radialError = distance - steering.CircleRadius;
                    var radialStrength = MathF.Min(MathF.Abs(radialError), 1f) * MathF.Sign(radialError);
                    steer += direction * radialStrength * steering.CircleWeight;

                    var tangent = new Vector2(-direction.Y, direction.X) * steering.CircleDirection;
                    steer += tangent * steering.CircleWeight;

                    if (enableWanderAndAvoid && distance < steering.AvoidRadius) {
                        var strength = (steering.AvoidRadius - distance) / steering.AvoidRadius;
                        steer -= direction * strength * steering.AvoidWeight;
                    }
                }
            }

            if (enableWanderAndAvoid) {
                var processedNeighbors = 0;
                var avoidRadiusSquared = steering.AvoidRadius * steering.AvoidRadius;
                foreach (var (other, _) in EnumerateNearby(steeringGrid, cellSize, entity.Position, steering.AvoidRadius)) {
                    if (other.Id == entity.Id) {
                        continue;
                    }

                    processedNeighbors++;
                    if (processedNeighbors > MaxNeighborsPerEntity) {
                        break;
                    }

                    var away = selfPosition - Flat(other.Position);
                    var distanceSquared = away.LengthSquared();
                    if (distanceSquared > MinDistanceSquared && distanceSquared < avoidRadiusSquared) {
                        var distance = MathF.Sqrt(distanceSquared);
                        var strength = (steering.AvoidRadius - distance) / steering.AvoidRadius;
                        steer += away / distance * strength * steering.AvoidWeight;
                    }
                }
            }

            if (enableWanderAndAvoid && steering.WanderWeight > 0f) {
                var jitter = ((float)Random.Shared.NextDouble() * 2f - 1f) * steering.WanderJitter * deltaTime;
                steering.WanderAngle += jitter;
                var wanderDirection = new Vector2(MathF.Cos(steering.WanderAngle), MathF.Sin(steering.WanderAngle));
                steer += wanderDirection * steering.WanderWeight;
            }

            if (steer.LengthSquared() > MinDistanceSquared) {
                var direction = Vector2.Normalize(steer);
                velocity.X = direction.X * steering.MoveSpeed;
                velocity.Z = direction.Y * steering.MoveSpeed;
            } else {
                velocity.X = 0f;
                velocity.Z = 0f;
            }
        }
    }

    private static Entity? FindNearestTarget(Vector2 entityPosition, List<(Entity Entity, InputComponent Component)> targets, out float nearestDistanceSquared) {
        Entity? nearest = null;
        nearestDistanceSquared = float.MaxValue;

        foreach (var (target, _) in targets) {
            var distanceSquared = (Flat(target.Position) - entityPosition).LengthSquared();
            if (distanceSquared < nearestDistanceSquared) {
                nearestDistanceSquared = distanceSquared;
                nearest = target;
            }
        }

        return nearest;
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

    private static void ApplyNoPlayerSteering(
        List<(Entity Entity, SteeringComponent Component)> steered,
        Dictionary<(int X, int Y), List<(Entity Entity, SteeringComponent Component)>> steeringGrid,
        float cellSize
    ) {
        foreach (var (entity, steering) in steered) {
            if (!entity.TryGetComponent<VelocityComponent>(out var velocity)) {
                continue;
            }

            var selfPosition = Flat(entity.Position);
            var steer = Vector2.Zero;
            var avoidRadiusSquared = steering.AvoidRadius * steering.AvoidRadius;
            var processedNeighbors = 0;
            foreach (var (other, _) in EnumerateNearby(steeringGrid, cellSize, entity.Position, steering.AvoidRadius)) {
                if (other.Id == entity.Id) {
                    continue;
                }

                processedNeighbors++;
                if (processedNeighbors > MaxNeighborsPerEntity) {
                    break;
                }

                var away = selfPosition - Flat(other.Position);
                var distanceSquared = away.LengthSquared();
                if (distanceSquared > MinDistanceSquared && distanceSquared < avoidRadiusSquared) {
                    var distance = MathF.Sqrt(distanceSquared);
                    var strength = (steering.AvoidRadius - distance) / steering.AvoidRadius;
                    steer += away / distance * strength * steering.AvoidWeight;
                }
            }

            if (steer.LengthSquared() > MinDistanceSquared) {
                var direction = Vector2.Normalize(steer);
                velocity.X = direction.X * steering.MoveSpeed;
                velocity.Z = direction.Y * steering.MoveSpeed;
            } else {
                velocity.X = 0f;
                velocity.Z = 0f;
            }
        }
    }

    private static Dictionary<(int X, int Y), List<(Entity Entity, SteeringComponent Component)>> BuildSteeringGrid(
        List<(Entity Entity, SteeringComponent Component)> steered,
        float cellSize
    ) {
        var grid = new Dictionary<(int X, int Y), List<(Entity Entity, SteeringComponent Component)>>();
        foreach (var pair in steered) {
            var key = GetCellKey(Flat(pair.Entity.Position), cellSize);
            if (!grid.TryGetValue(key, out var bucket)) {
                bucket = [];
                grid[key] = bucket;
            }

            bucket.Add(pair);
        }

        return grid;
    }

    private static IEnumerable<(Entity Entity, SteeringComponent Component)> EnumerateNearby(
        Dictionary<(int X, int Y), List<(Entity Entity, SteeringComponent Component)>> grid,
        float cellSize,
        Vector3 center,
        float radius
    ) {
        var (centerX, centerY) = GetCellKey(Flat(center), cellSize);
        var cellRadius = (int)MathF.Ceiling(radius / cellSize);
        for (var y = centerY - cellRadius; y <= centerY + cellRadius; y++) {
            for (var x = centerX - cellRadius; x <= centerX + cellRadius; x++) {
                if (!grid.TryGetValue((x, y), out var bucket)) {
                    continue;
                }

                foreach (var entry in bucket) {
                    yield return entry;
                }
            }
        }
    }

    private static (int X, int Y) GetCellKey(Vector2 position, float cellSize) {
        var x = (int)MathF.Floor(position.X / cellSize);
        var y = (int)MathF.Floor(position.Y / cellSize);
        return (x, y);
    }

    private static Vector2 Flat(Vector3 position) => new(position.X, position.Z);
}
