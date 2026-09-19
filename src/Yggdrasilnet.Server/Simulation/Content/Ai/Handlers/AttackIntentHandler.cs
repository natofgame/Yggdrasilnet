using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai.Handlers;

public sealed class AttackIntentHandler : AbstractAiIntentHandler {
    private const float CastRange = 1.2f;

    private const float MinCooldown = 1f; // aggressivity = 10
    private const float MaxCooldown = 5f; // aggressivity = 0
    private const float CooldownJitter = 3f; // +/- variation aléatoire

    protected override void OnNoTarget(SteeringComponent steering) {
        steering.MoveSpeed = 0f;
    }

    protected override void OnHasTarget(World.World world, AiContext context, World.Entity entity, SteeringComponent steering) {
        if (IsCasting(entity)) {
            steering.MoveSpeed = 0f;
            return;
        }

        if (context.TargetDistance <= CastRange) {
            steering.MoveSpeed = 0f;
            entity.AddComponent(new CastSpellIntentComponent { SpellIndex = 0 });

            if (entity.TryGetComponent<AiCombatStateComponent>(out var combatState)) {
                combatState.PostAttackTimer = ResolveCooldown(entity);
            }
            return;
        }

        steering.SeekWeight = 1f;
        steering.CircleWeight = 0.3f;
        steering.MoveSpeed = 7f;
    }

    private static float ResolveCooldown(World.Entity entity) {
        var aggressivity = entity.TryGetComponent<AiComponent>(out var ai) ? ai.Aggressivity : 5f;
        var t = Math.Clamp(aggressivity / 10f, 0f, 1f);

        var baseCooldown = MathF.Max(0.05f, float.Lerp(MaxCooldown, MinCooldown, t));
        var jitter = ((float)Random.Shared.NextDouble() * 2f - 1f) * CooldownJitter;

        return MathF.Max(0.05f, baseCooldown + jitter);
    }
}