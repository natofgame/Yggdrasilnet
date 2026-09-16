using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content.Spell;

public sealed class SpellCastPipeline() {
    private readonly SpellEffectApplier _effects = new();
    
    public void Cast(World.World world, World.Entity caster, SpellDefinition spell) {
        if (!SpellCastValidator.TryCreate(world, caster, spell, out var context)) {
            return;
        }
        _effects.ApplySelf(world, context, caster, spell);

        foreach (var target in SpellTargetResolver.Resolve(world, context, spell.Targeting, caster)) { 
            _effects.ApplyToTarget(world, context, target, spell);
        }
    }
}