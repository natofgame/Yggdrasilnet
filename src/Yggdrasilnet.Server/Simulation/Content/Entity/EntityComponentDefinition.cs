using System.Text.Json.Serialization;

namespace Yggdrasilnet.Server.Simulation.Content.Entity;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(InputComponentDefinition), "input")]
[JsonDerivedType(typeof(VelocityComponentDefinition), "velocity")]
[JsonDerivedType(typeof(HealthComponentDefinition), "health")]
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