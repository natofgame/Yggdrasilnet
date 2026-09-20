using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect.Handlers;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Effect;

public sealed class SpellEffectApplier {
    private readonly IReadOnlyDictionary<SpellEffectType, ISpellEffectHandler> _handlers = new OrderedDictionary<SpellEffectType, ISpellEffectHandler>() {
        { SpellEffectType.Damage, new DamageEffectHandler() },
        { SpellEffectType.Heal, new HealEffectHandler() },
        { SpellEffectType.Dash, new DashEffectHandler() },
        { SpellEffectType.Punch, new  PunchEffectHandler()  },
    };
    
    public void ApplySelf(World.World world, SpellCastContext context, World.Entity caster, SpellDefinition spell, SpellPhase phase) {
        foreach (var e in spell.Effects.Where(e => e.Target == SpellEffectTarget.Self)) {
            switch (phase) {
                case SpellPhase.Anticipation when !e.ActiveOnAnticipation:
                case SpellPhase.Strike when e.ActiveOnAnticipation:
                    return;
                default:
                    _handlers[e.Type].Apply(world, context, caster, e.Amount, e.Duration);
                    break;
            }
        }
    }

    public void ApplyToTarget(World.World world, SpellCastContext context, World.Entity target, SpellDefinition spell, SpellPhase phase) {
        if (!SpellCastValidator.IsLiving(target)) {
            return;
        }

        foreach (var e in spell.Effects.Where(e => e.Target == SpellEffectTarget.Target)) {
            if (phase == SpellPhase.Anticipation && !e.ActiveOnAnticipation) {
                return;
            }
            _handlers[e.Type].Apply(world, context, target, e.Amount, e.Duration);
        }
    }
}