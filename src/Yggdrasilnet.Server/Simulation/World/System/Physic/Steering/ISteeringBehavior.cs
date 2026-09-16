using System.Numerics;

namespace Yggdrasilnet.Server.Simulation.World.System.Steering;


internal interface ISteeringBehavior {
    void Apply(SteeringContext context, SteeringAgent agent, ref Vector2 steer);
}
