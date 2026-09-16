using System.Numerics;

namespace Yggdrasilnet.Server.Simulation.World.System.Steering.Behaviors;

internal sealed class TargetAvoidSteeringBehavior : ISteeringBehavior {
    public void Apply(SteeringContext context, SteeringAgent agent, ref Vector2 steer) {
        if (!context.HasDirection || !context.EnableWanderAndAvoid) {
            return;
        }

        if (context.Distance >= agent.Steering.AvoidRadius) {
            return;
        }

        var strength = (agent.Steering.AvoidRadius - context.Distance) / agent.Steering.AvoidRadius;
        steer -= context.Direction * strength * agent.Steering.AvoidWeight;
    }
}
