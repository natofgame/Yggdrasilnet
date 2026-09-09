using System.Linq;
using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System;

public sealed class SteeringSystem : ISystem {
    public void Update(World world, float deltaTime) {
        var steered = world.Query<SteeringComponent>().ToList();
        var targets = world.Query<InputComponent>().ToList();

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

            foreach (var (other, _) in steered) {
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

    private static Vector2 Flat(Vector3 position) => new(position.X, position.Z);
}
