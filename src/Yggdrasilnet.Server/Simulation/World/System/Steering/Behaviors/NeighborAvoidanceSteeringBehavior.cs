using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System.Steering.Behaviors;

internal sealed class NeighborAvoidanceSteeringBehavior : ISteeringBehavior {
    public void Apply(SteeringContext context, SteeringAgent agent, ref Vector2 steer) {
        if (!context.EnableWanderAndAvoid || !HasAvoidance(agent.Steering)) {
            return;
        }

        context.ApplyNeighborAvoidance(agent, ref steer);
    }

    private static bool HasAvoidance(SteeringComponent steering) =>
        steering.AvoidRadius > 0f && steering.AvoidWeight != 0f;
}
