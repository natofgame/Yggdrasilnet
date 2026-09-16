using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content.Spell;

public sealed class SpellDeliveryResolver {
    private readonly Dictionary<SpellType, Action<World.World, SpellCastContext, SpellDefinition>> _handlers;

    public SpellDeliveryResolver(SpellProjectileSpawner projectiles, SpellEffectResolver effects) {
        _handlers = new Dictionary<SpellType, Action<World.World, SpellCastContext, SpellDefinition>> {
            [SpellType.Projectile] = projectiles.Spawn,
            [SpellType.Self] = Instant,
            [SpellType.Melee] = Instant,
            [SpellType.Targeted] = Instant
        };
        return;

        void Instant(World.World world, SpellCastContext context, SpellDefinition spell) =>
            effects.Apply(world, context, spell);
    }

    public void Deliver(World.World world, SpellCastContext context, SpellDefinition spell) {
        if (!world.TryGetEntity(context.CasterEntityId, out var caster) || !SpellCastValidator.IsLiving(caster)) {
            return;
        }
        _handlers[context.SpellType](world, context, spell);
    }
}
