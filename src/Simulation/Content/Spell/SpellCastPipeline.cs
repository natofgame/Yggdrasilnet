using Yggdrasilnet.Gameplay.Enums;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;

namespace Yggdrasilnet.Server.Simulation.Content.Spell;

public sealed class SpellCastPipeline(SpellEffectApplier effects) {
    public void Cast(World.World world, World.Entity caster, SpellDefinition spell, SpellPhase phase) {
        if (!SpellCastValidator.TryCreate(world, caster, spell, out var context)) {
            return;
        }

        effects.ApplySelf(world, context, caster, spell, phase);
        if (spell.Projectile is { } projectile) {
            if (phase == SpellPhase.Strike) {
                ProjectileSpawner.TrySpawn(world, caster, context, spell, projectile);
            }
            return;
        }

        foreach (var target in SpellTargetResolver.Resolve(world, context, spell.Targeting, caster).ToList()) {
            effects.ApplyToTarget(world, context, target, spell, phase);
        }
    }
}