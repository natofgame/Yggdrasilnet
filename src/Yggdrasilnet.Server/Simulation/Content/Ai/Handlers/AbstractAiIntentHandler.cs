using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content.Ai.Handlers;

public abstract class AbstractAiIntentHandler : IAiIntentHandler {
    public void Apply(World.World world, AiContext context, World.Entity entity) {
        if (!entity.TryGetComponent<SteeringComponent>(out var steering)) {
            return;
        }

        ResetIntentState(steering);

        if (!context.HasTarget) {
            OnNoTarget(steering);
            return;
        }

        OnHasTarget(world, context, entity, steering);
    }
    private static void ResetIntentState(SteeringComponent steering) {
        steering.InputDirection = Vector2.Zero;
        steering.SeekWeight = 0f;
        steering.CircleWeight = 0f;
        steering.HasDash = false;
    }

    protected abstract void OnNoTarget(SteeringComponent steering);
    protected abstract void OnHasTarget(World.World world, AiContext context, World.Entity entity, SteeringComponent steering);

    protected static bool IsCasting(World.Entity entity) =>
        entity.TryGetComponent<ActionStateComponent>(out var action) && action.Phase != SpellPhase.None;

}