using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.System.Physic.Steering;

namespace Yggdrasilnet.Server.Simulation.World.System.Steering.Behaviors;

/// <summary>Pulls the agent straight toward the current target.</summary>
internal sealed class SeekSteeringBehavior : ISteeringBehavior {
    public void Apply(SteeringContext context, SteeringAgent agent, ref Vector2 steer) {
        if (!context.HasDirection) {
            return;
        }

        steer += context.Direction * agent.Steering.SeekWeight;
    }
}
