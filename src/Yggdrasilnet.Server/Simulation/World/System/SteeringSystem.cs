using System.Linq;
using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System;

public sealed class SteeringSystem : ISystem {
    public void Update(World world, float deltaTime) {
        var steered = world.Query<SteeringComponent>().ToList();
        var targets = world.Query<InputComponent>().ToList();
        if (steered.Count == 0) {
            return;
        }

        var maxAvoidRadius = steered.Max(pair => pair.Component.AvoidRadius);
        var cellSize = MathF.Max(0.1f, maxAvoidRadius);
        var steeringGrid = BuildSteeringGrid(steered, cellSize);

        foreach (var (entity, steering) in steered) {
            if (!entity.TryGetComponent<VelocityComponent>(out var velocity)) {
                continue;
            }

            var steer = Vector2.Zero;

            var target = FindNearestTarget(entity, targets);
            if (target != null) {
                var toTarget = Flat(target.Position) - Flat(entity.Position);
                var distance = toTarget.Length();

                if (distance > 0.0001f) {
                    var direction = toTarget / distance;

                    steer += direction * steering.SeekWeight;

                    var radialError = distance - steering.CircleRadius;
                    var radialStrength = MathF.Min(MathF.Abs(radialError), 1f) * MathF.Sign(radialError);
                    steer += direction * radialStrength * steering.CircleWeight;

                    var tangent = new Vector2(-direction.Y, direction.X) * steering.CircleDirection;
                    steer += tangent * steering.CircleWeight;

                    if (distance < steering.AvoidRadius) {
                        var strength = (steering.AvoidRadius - distance) / steering.AvoidRadius;
                        steer -= direction * strength * steering.AvoidWeight;
                    }
                }
            }

            foreach (var (other, _) in EnumerateNearby(steeringGrid, cellSize, entity.Position, steering.AvoidRadius)) {
                if (other.Id == entity.Id) {
                    continue;
                }

                var away = Flat(entity.Position) - Flat(other.Position);
                var distance = away.Length();
                if (distance > 0.0001f && distance < steering.AvoidRadius) {
                    var strength = (steering.AvoidRadius - distance) / steering.AvoidRadius;
                    steer += away / distance * strength * steering.AvoidWeight;
                }
            }

            if (steering.WanderWeight > 0f) {
                var jitter = ((float)Random.Shared.NextDouble() * 2f - 1f) * steering.WanderJitter * deltaTime;
                steering.WanderAngle += jitter;
                var wanderDirection = new Vector2(MathF.Cos(steering.WanderAngle), MathF.Sin(steering.WanderAngle));
                steer += wanderDirection * steering.WanderWeight;
            }

            if (steer.LengthSquared() > 0.0001f) {
                var direction = Vector2.Normalize(steer);
                velocity.X = direction.X * steering.MoveSpeed;
                velocity.Z = direction.Y * steering.MoveSpeed;
            } else {
                velocity.X = 0f;
                velocity.Z = 0f;
            }
        }
    }

    private static Entity? FindNearestTarget(Entity entity, List<(Entity Entity, InputComponent Component)> targets) {
        Entity? nearest = null;
        var bestDistanceSquared = float.MaxValue;

        foreach (var (target, _) in targets) {
            var distanceSquared = (Flat(target.Position) - Flat(entity.Position)).LengthSquared();
            if (distanceSquared < bestDistanceSquared) {
                bestDistanceSquared = distanceSquared;
                nearest = target;
            }
        }

        return nearest;
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
