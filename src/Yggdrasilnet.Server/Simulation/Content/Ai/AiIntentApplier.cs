using Yggdrasilnet.Server.Simulation.Content.Ai.Handlers;

namespace Yggdrasilnet.Server.Simulation.Content.Ai;

public sealed class AiIntentApplier {
    private readonly Dictionary<AiIntentType, IAiIntentHandler> _handlers = new() {
        [AiIntentType.Attack] = new AttackIntentHandler(),
        [AiIntentType.Flee]   = new FleeIntentHandler(),
        [AiIntentType.Flank]  = new FlankIntentHandler()
    };

    public void Apply(World.World world, AiContext ctx, World.Entity entity, AiIntentType intent) {
        if (_handlers.TryGetValue(intent, out var handler)) {
            handler.Apply(world, ctx, entity);
        }
    }
}