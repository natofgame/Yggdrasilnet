using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai.Behaviors;

public sealed class AttackBehavior : AiBehavior {
    public float Speed { get; set; } = 7f;
    public float CastRange { get; set; } = 1.2f;
    public float CircleWeight { get; set; } = 0.3f;
    public byte SpellIndex { get; set; }
    public float MinCooldown { get; set; } = 5f;
    public float MaxCooldown { get; set; } = 10f;
    public float CooldownJitter { get; set; } = 3f;

    public override float Score(AiComponent ai) {
        if (!ai.HasTarget || !ai.HasAttackToken || ai.AttackCooldown > 0f) {
            return 0f;
        }

        var aggro = Clamp01(ai.Aggressivity / 10f);
        var impulsivity = Clamp01(ai.Impulsivity / 10f);
        var score = ai.HealthRatio * aggro
                    + (1f - ai.TargetHealthRatio) * 0.25f
                    + impulsivity * (1f - ai.HealthRatio) * 0.25f;

        return MathF.Max(0.3f, score);
    }

    public override void Tick(World.World world, World.Entity entity, AiComponent ai, SteeringComponent steering, float dt) {
        if (ai.TargetDistance <= CastRange) {
            steering.MoveSpeed = 0f;
            entity.AddComponent(new CastSpellIntentComponent { SpellIndex = SpellIndex });
            ai.AttackCooldown = ResolveCooldown(ai.Aggressivity);
            return;
        }

        steering.SeekWeight = 1f;
        steering.CircleWeight = CircleWeight;
        steering.MoveSpeed = Speed;
    }

    private float ResolveCooldown(float aggressivity) {
        var t = Clamp01(aggressivity / 10f);
        var baseCooldown = MathF.Max(0.05f, float.Lerp(MaxCooldown, MinCooldown, t));
        var jitter = ((float)Random.Shared.NextDouble() * 2f - 1f) * CooldownJitter;
        return MathF.Max(0.05f, baseCooldown + jitter);
    }

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
}
