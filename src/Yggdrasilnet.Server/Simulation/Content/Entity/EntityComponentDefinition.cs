using System.Text.Json.Serialization;
using Yggdrasilnet.Server.Simulation.World.Component;

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
[JsonDerivedType(typeof(AiComponentDefinition), "ai")]
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

public sealed class AiComponentDefinition : EntityComponentDefinition {
    public float Prudence { get; set; } = 0f;
    public float Impulsivity { get; set; } = 0f;
    public float Aggressivity { get; set; } = 0f;
    public float Courage { get; set; } = 0f;
    public float Curiosity { get; set; } = 0f;
    public float Discipline { get; set; } = 0f;

    public float SightRange { get; set; } = 20f;
    public float PerceptionInterval { get; set; } = 0.2f;

    public float SurprisedDuration { get; set; } = 0.4f;
    public float FearDuration { get; set; } = 2.5f;
    public float FearRadius { get; set; } = 8f;
    public float WanderRadius { get; set; } = 6f;
    public float WanderArriveDistance { get; set; } = 0.5f;
    public float WanderIdleDuration { get; set; } = 2f;
}