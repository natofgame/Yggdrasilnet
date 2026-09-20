using System.Text.Json.Serialization;
using Yggdrasilnet.Server.Simulation.Content.Ai.Behaviors;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WanderBehavior), "wander")]
[JsonDerivedType(typeof(AttackBehavior), "attack")]
[JsonDerivedType(typeof(HoldBehavior), "hold")]
[JsonDerivedType(typeof(FleeBehavior), "flee")]
[JsonDerivedType(typeof(FlankBehavior), "flank")]
public abstract class AiBehavior {
    public abstract float Score(AiComponent ai);
    public abstract void Tick(World.World world, World.Entity entity, AiComponent ai, SteeringComponent steering, float dt);
}
