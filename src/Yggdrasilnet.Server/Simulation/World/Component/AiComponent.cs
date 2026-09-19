using System.Numerics;

namespace Yggdrasilnet.Server.Simulation.World.Component;

public class AiComponent : IComponent {
    public float Prudence { get; set; }
    public float Impulsivity { get; set; }
    public float Aggressivity { get; set; }
    public float Courage { get; set; }
    public float Curiosity { get; set; }
    public float Discipline { get; set; }
    
    public float SightRange = 20f;
    public float PerceptionInterval = 0.2f;
    public float PerceptionTimer;
    
    public int? TargetEntityId;
    public float TargetDistance;
    public Vector3 TargetDirection;
    public float TargetHealthRatio;
    public int AlliesNearby;
    public float DangerLevel;
    public bool HasTarget => TargetEntityId.HasValue;
}