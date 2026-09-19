using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai.Behaviors;

public sealed class FleeBehavior : AiBehavior {
    public float Speed { get; set; } = 1f;
    public float DashMultiplier { get; set; } = 1.5f;
    public float DashDuration { get; set; } = 0.2f;

    public override float Score(AiComponent ai) {
        if (!ai.HasTarget) {
            return 0f;
        }

        var prudence = Clamp01(ai.Prudence / 10f);
        var courage = Clamp01(ai.Courage / 10f);
        return prudence * (1f - courage) * (1f - ai.HealthRatio);
    }

    public override void Tick(World.Entity entity, AiComponent ai, SteeringComponent steering, float dt) {
        steering.SeekWeight = -1f;
        steering.MoveSpeed = Speed;

        var away = new Vector2(-ai.TargetDirection.X, -ai.TargetDirection.Z);
        if (away.LengthSquared() > 0.0001f) {
            away = Vector2.Normalize(away);
        }

        steering.HasDash = true;
        steering.DashDirection = away;
        steering.DashSpeed = Speed * DashMultiplier;
        steering.DashTimer = DashDuration;
    }

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
}
