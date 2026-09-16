using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Effect.Handlers;

public class DashEffectHandler : ISpellEffectHandler {
    public void Apply(World.World world, SpellCastContext context, World.Entity actor, float amount, float duration) {
        if (!world.TryGetEntity(context.CasterEntityId, out var caster)) {
            return;
        }

        if (!caster.TryGetComponent<SteeringComponent>(out var steering)) {
            return;
        }

        var dir = steering.InputDirection;

        dir = dir.LengthSquared() > 0.0001f ? Vector2.Normalize(dir) : Vector2.UnitX;
        steering.HasDash = true;
        steering.DashDirection = dir;
        steering.DashSpeed = amount;
        steering.DashTimer = duration;
    }
}