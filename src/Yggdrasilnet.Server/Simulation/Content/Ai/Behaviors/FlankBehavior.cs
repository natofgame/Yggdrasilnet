using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai.Behaviors;

public sealed class FlankBehavior : AiBehavior {
    public float Speed { get; set; } = 1f;
    public float Distance { get; set; } = 3f;

    public override float Score(AiComponent ai) {
        if (!ai.HasTarget) {
            return 0f;
        }

        var curiosity = Clamp01(ai.Curiosity / 10f);
        return curiosity * Clamp01(ai.TargetDistance / 10f);
    }

    public override void Tick(World.Entity entity, AiComponent ai, SteeringComponent steering, float dt) {
        steering.CircleRadius = Distance;
        steering.CircleDirection = entity.Id % 2 == 0 ? 1f : -1f;
        steering.CircleWeight = 1f;
        steering.MoveSpeed = Speed;
    }

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
}
