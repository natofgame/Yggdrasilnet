using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai.Handlers;

public sealed class FlankIntentHandler : AbstractAiIntentHandler {
    private const float PreferredFlankDistance = 3f;

    protected override void OnNoTarget(SteeringComponent steering) {
        steering.MoveSpeed = 0f;
    }

    protected override void OnHasTarget(World.World world, AiContext context, World.Entity entity, SteeringComponent steering) {
        steering.CircleRadius = PreferredFlankDistance;
        steering.CircleDirection = entity.Id % 2 == 0 ? 1f : -1f;
        steering.CircleWeight = 1f;
        steering.MoveSpeed = 1f;
    }
}