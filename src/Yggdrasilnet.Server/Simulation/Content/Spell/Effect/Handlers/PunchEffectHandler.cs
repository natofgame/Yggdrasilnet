using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Effect.Handlers;

public class PunchEffectHandler : ISpellEffectHandler {
    public void Apply(World.World world, SpellCastContext context, World.Entity actor, float amount, float duration) {
        if (!actor.TryGetComponent<SteeringComponent>(out var steering)) {
            return;
        }

        var direction = new Vector2(context.Direction.X, context.Direction.Z);
        if (direction.LengthSquared() <= 0.0001f) {
            return;
        }

        direction = Vector2.Normalize(direction);
        steering.HasPunch = true;
        steering.PunchDirection = direction;
        steering.PunchOverrideSpeed = 20f;
        steering.PunchTimer = 0.06f;
        steering.PunchFreezeTimer = 0.3f;
    }
}