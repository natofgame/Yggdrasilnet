using System.Numerics;

namespace Yggdrasilnet.Server.Simulation.World.System.Steering.Behaviors;

internal sealed class CircleSteeringBehavior : ISteeringBehavior {
    public void Apply(SteeringContext context, SteeringAgent agent, ref Vector2 steer) {
        if (!context.HasDirection) {
            return;
        }

        var direction = context.Direction;
        var radialError = context.Distance - agent.Steering.CircleRadius;
        var radialStrength = MathF.Min(MathF.Abs(radialError), 1f) * MathF.Sign(radialError);
        steer += direction * radialStrength * agent.Steering.CircleWeight;

        var tangent = new Vector2(-direction.Y, direction.X) * agent.Steering.CircleDirection;
        steer += tangent * agent.Steering.CircleWeight;
    }
}
