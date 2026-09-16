using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect.Handlers;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Effect;

public sealed class SpellEffectApplier {
    private readonly IReadOnlyDictionary<SpellEffectType, ISpellEffectHandler> _handlers = new OrderedDictionary<SpellEffectType, ISpellEffectHandler>() {
        { SpellEffectType.Damage, new DamageEffectHandler() },
        { SpellEffectType.Heal, new HealEffectHandler() },
        { SpellEffectType.Dash, new DashEffectHandler() },
        { SpellEffectType.Punch, new  PunchEffectHandler()  },
    };
    
    public void ApplySelf(World.World world, SpellCastContext context, World.Entity caster, SpellDefinition spell) {
        foreach (var e in spell.Effects.Where(e => e.Target == SpellEffectTarget.Self)) {
            _handlers[e.Type].Apply(world, context, caster, e.Amount, e.Duration);
        }
    }

    public void ApplyToTarget(World.World world, SpellCastContext context, World.Entity target, SpellDefinition spell) {
        if (!SpellCastValidator.IsLiving(target)) {
            return;
        }

        foreach (var e in spell.Effects.Where(e => e.Target == SpellEffectTarget.Target)) {
            _handlers[e.Type].Apply(world, context, target, e.Amount, e.Duration);
        }
    }
}