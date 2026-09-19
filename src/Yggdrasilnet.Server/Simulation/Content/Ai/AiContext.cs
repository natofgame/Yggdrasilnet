
using System.Numerics;

namespace Yggdrasilnet.Server.Simulation.Content.Ai;

public class AiContext {
    public int Id;
    public Vector3 Position;
    public float TargetHealthRatio;
    public float HealthRatio;
    public bool HasTarget;

    public float TargetDistance;
    public Vector3 TargetDirection;

    public float DangerLevel;
    public int AlliesNearby;

    public bool IsCasting;
    public bool RecentlyAttacked;
}