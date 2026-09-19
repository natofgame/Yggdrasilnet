using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai.Behaviors;

public sealed class AttackBehavior : IAiBehavior {
    private const float CastRange = 1.2f;
    private const float MinCooldown = 1f;
    private const float MaxCooldown = 5f;
    private const float CooldownJitter = 3f;

    public float Score(AiComponent ai) {
        if (!ai.HasTarget || ai.AttackCooldown > 0f) {
            return 0f;
        }

        var aggro = Clamp01(ai.Aggressivity / 10f);
        var impulsivity = Clamp01(ai.Impulsivity / 10f);
        return ai.HealthRatio * aggro
               + (1f - ai.TargetHealthRatio) * 0.25f
               + impulsivity * (1f - ai.HealthRatio) * 0.25f;
    }

    public void Tick(World.Entity entity, AiComponent ai, SteeringComponent steering, float dt) {
        if (ai.TargetDistance <= CastRange) {
            steering.MoveSpeed = 0f;
            entity.AddComponent(new CastSpellIntentComponent { SpellIndex = 0 });
            ai.AttackCooldown = ResolveCooldown(ai.Aggressivity);
            return;
        }

        steering.SeekWeight = 1f;
        steering.CircleWeight = 0.3f;
        steering.MoveSpeed = 7f;
    }

    private static float ResolveCooldown(float aggressivity) {
        var t = Clamp01(aggressivity / 10f);
        var baseCooldown = MathF.Max(0.05f, float.Lerp(MaxCooldown, MinCooldown, t));
        var jitter = ((float)Random.Shared.NextDouble() * 2f - 1f) * CooldownJitter;
        return MathF.Max(0.05f, baseCooldown + jitter);
    }

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
}
