using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.System.Steering;

namespace Yggdrasilnet.Server.Simulation.World.System.Physic.Steering;

internal interface ISteeringBehavior {
    void Apply(SteeringContext context, SteeringAgent agent, ref Vector2 steer);
}
