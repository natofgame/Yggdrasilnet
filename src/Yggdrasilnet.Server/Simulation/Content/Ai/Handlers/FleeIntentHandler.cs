using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai.Handlers;

public class FleeIntentHandler : AbstractAiIntentHandler {
    protected override void OnNoTarget(SteeringComponent steering) {
        steering.MoveSpeed = 0.5f;
    }

    protected override void OnHasTarget(World.World world, AiContext context, World.Entity entity, SteeringComponent steering) {
        steering.SeekWeight = -1f;
        steering.MoveSpeed = 1f;

        var away = new Vector2(-context.TargetDirection.X, -context.TargetDirection.Z);
        if (away.LengthSquared() > 0.0001f) {
            away = Vector2.Normalize(away);
        }

        steering.HasDash = true;
        steering.DashDirection = away;
        steering.DashSpeed = steering.MoveSpeed * 1.5f;
        steering.DashTimer = 0.2f;
    }
}