
using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Effect.Handlers;

public class DashEffectHandler : ISpellEffectHandler {
    public void Apply(World.World world, SpellCastContext context, World.Entity actor, float amount, float duration) {
        if (!world.TryGetEntity(context.CasterEntityId, out var caster)) {
            return;
        }

        Vector2? direction = null;
       
        if (caster.TryGetComponent<AiComponent>(out var ai)) {
            direction = new Vector2(
                ai.TargetDirection.X,
                ai.TargetDirection.Z
            );
        }
        
        if (!caster.TryGetComponent<SteeringComponent>(out var steering)) {
            return;
        }

        direction ??= steering.InputDirection;

        direction = direction.Value.LengthSquared() > 0.0001f ? Vector2.Normalize(direction.Value) : Vector2.UnitX;
        steering.HasDash = true;
        steering.DashDirection = direction.Value;
        steering.DashSpeed = amount;
        steering.DashTimer = duration;
    }
}