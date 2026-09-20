using Yggdrasilnet.Gameplay.Enums;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect.Handlers;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Effect;

public sealed class SpellEffectApplier {
    private readonly IReadOnlyDictionary<SpellEffectType, ISpellEffectHandler> _handlers = new OrderedDictionary<SpellEffectType, ISpellEffectHandler>() {
        { SpellEffectType.Damage, new DamageEffectHandler() },
        { SpellEffectType.Heal, new HealEffectHandler() },
        { SpellEffectType.Dash, new DashEffectHandler() },
        { SpellEffectType.Punch, new PunchEffectHandler() },
    };

    public void ApplySelf(World.World world, SpellCastContext context, World.Entity caster, SpellDefinition spell, SpellPhase phase) {
        foreach (var effect in spell.Effects) {
            if (effect.Target != SpellEffectTarget.Self || !IsActive(effect, phase)) {
                continue;
            }

            _handlers[effect.Type].Apply(world, context, caster, effect.Amount, effect.Duration);
        }
    }

    public void ApplyToTarget(
        World.World world,
        SpellCastContext context,
        World.Entity target,
        SpellDefinition spell,
        SpellPhase phase,
        float amountScale = 1f) {

        if (!SpellCastValidator.IsLiving(target)) {
            return;
        }

        foreach (var effect in spell.Effects) {
            if (effect.Target != SpellEffectTarget.Target || !IsActive(effect, phase)) {
                continue;
            }

            var amount = effect.Type is SpellEffectType.Damage or SpellEffectType.Heal
                ? effect.Amount * amountScale
                : effect.Amount;

            _handlers[effect.Type].Apply(world, context, target, amount, effect.Duration);
        }
    }
    
    private static bool IsActive(SpellEffectDefinition effect, SpellPhase phase) => phase switch {
        SpellPhase.Anticipation => effect.ActiveOnAnticipation,
        SpellPhase.Strike => !effect.ActiveOnAnticipation,
        _ => false,
    };
}