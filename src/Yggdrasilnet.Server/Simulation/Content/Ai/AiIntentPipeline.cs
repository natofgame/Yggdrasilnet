
namespace Yggdrasilnet.Server.Simulation.Content.Ai;

public sealed class AiIntentPipeline {
    private readonly AiIntentApplier _applier = new();

    public void Tick(World.World world, World.Entity entity, float deltaTime) {
        if (!AiIntentValidator.TryCreate(world, entity, deltaTime, out var context)) {
            return;
        }

        var intent = AiIntentResolver.Resolve(world, context);
        _applier.Apply(world, context, entity, intent);
    }
}