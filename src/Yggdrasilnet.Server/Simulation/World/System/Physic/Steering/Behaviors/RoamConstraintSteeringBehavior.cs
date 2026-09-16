using System.Numerics;

namespace Yggdrasilnet.Server.Simulation.World.System.Steering.Behaviors;

internal sealed class RoamConstraintSteeringBehavior : ISteeringBehavior {
    public void Apply(SteeringContext context, SteeringAgent agent, ref Vector2 steer) {
        var roamRadius = agent.Steering.RoamRadius;
        if (roamRadius <= 0f) {
            return;
        }

        var spawn = new Vector2(agent.Steering.SpawnX, agent.Steering.SpawnZ);
        var fromSpawn = agent.Position - spawn;
        var distanceSquared = fromSpawn.LengthSquared();
        if (distanceSquared <= SteeringConstants.MinDistanceSquared) {
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
}
