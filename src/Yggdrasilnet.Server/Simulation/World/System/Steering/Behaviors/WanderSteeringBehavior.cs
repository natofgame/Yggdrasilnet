using System.Numerics;

namespace Yggdrasilnet.Server.Simulation.World.System.Steering.Behaviors;

internal sealed class WanderSteeringBehavior : ISteeringBehavior {
    public void Apply(SteeringContext context, SteeringAgent agent, ref Vector2 steer) {
        if (agent.Steering.WanderWeight <= 0f) {
            return;
        }

        var jitter = ((float)Random.Shared.NextDouble() * 2f - 1f) * agent.Steering.WanderJitter * context.DeltaTime;
        agent.Steering.WanderAngle += jitter;
        var wanderDirection = new Vector2(MathF.Cos(agent.Steering.WanderAngle), MathF.Sin(agent.Steering.WanderAngle));
        steer += wanderDirection * agent.Steering.WanderWeight;
    }
}
