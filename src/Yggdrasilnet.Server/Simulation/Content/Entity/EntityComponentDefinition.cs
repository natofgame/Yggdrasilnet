using System.Text.Json.Serialization;

namespace Yggdrasilnet.Server.Simulation.Content.Entity;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(InputComponentDefinition), "input")]
[JsonDerivedType(typeof(VelocityComponentDefinition), "velocity")]
[JsonDerivedType(typeof(HealthComponentDefinition), "health")]
[JsonDerivedType(typeof(SteeringComponentDefinition), "steering")]
[JsonDerivedType(typeof(TargetComponentDefinition), "target")]
[JsonDerivedType(typeof(SpellbookComponentDefinition), "spellbook")]
[JsonDerivedType(typeof(AutocastComponentDefinition), "autocast")]
[JsonDerivedType(typeof(CollisionComponentDefinition), "collision")]
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
    public float RoamRadius { get; set; }
    public float SpawnSpacing { get; set; }
}

public sealed class TargetComponentDefinition : EntityComponentDefinition {
    public float Range { get; set; }
    public bool AutocastersOnly { get; set; }
}

public sealed class SpellbookComponentDefinition : EntityComponentDefinition {
    public List<string> Spells { get; set; } = [];
}

public sealed class AutocastComponentDefinition : EntityComponentDefinition {
    public byte SpellIndex { get; set; }
    public float IntervalSeconds { get; set; } = 2f;
}

public sealed class CollisionComponentDefinition : EntityComponentDefinition {
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public bool IsTrigger { get; set; } = false;
    public string Layer { get; set; } = "world";
    public string Mask { get; set; } = "all";
}