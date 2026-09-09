using System.Text.Json.Serialization;

namespace Yggdrasilnet.Server.Simulation.Content.Entity;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(InputComponentDefinition), "input")]
[JsonDerivedType(typeof(VelocityComponentDefinition), "velocity")]
[JsonDerivedType(typeof(HealthComponentDefinition), "health")]
[JsonDerivedType(typeof(SteeringComponentDefinition), "steering")]
public abstract class EntityComponentDefinition;

public sealed class VelocityComponentDefinition : EntityComponentDefinition {
    public float Speed { get; set; }
}

public sealed class HealthComponentDefinition : EntityComponentDefinition {
    public float Max { get; set; }
}

public sealed class InputComponentDefinition : EntityComponentDefinition {
    public float Speed { get; set; }
}

public sealed class SteeringComponentDefinition : EntityComponentDefinition {
    public float Speed { get; set; }
    public float AvoidRadius { get; set; }
    public float AvoidWeight { get; set; } = 1f;
    public float SeekWeight { get; set; } = 1f;
    public float CircleRadius { get; set; }
    public float CircleWeight { get; set; } = 1f;
    public float WanderWeight { get; set; }
    public float WanderJitter { get; set; } = 1f;
}