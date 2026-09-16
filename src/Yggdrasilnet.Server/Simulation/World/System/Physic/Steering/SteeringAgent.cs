using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System.Steering;

internal readonly record struct SteeringAgent(Entity Entity, SteeringComponent Steering, VelocityComponent Velocity, Vector2 Position);
