using Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Spell;

public sealed class SpellProjectileSpawner(DefinitionRegistry<EntityDefinition> entityDefinitions) {
    public void Spawn(World.World world, SpellCastContext context, SpellDefinition spell) {
        if (!entityDefinitions.TryGetIndex(spell.ProjectileEntityId, out var index)) {
            return;
        }

        var projectile = world.Spawn(context.Origin);
        projectile.DefinitionIndex = index;
        projectile.AddComponent(new VelocityComponent {
            X = context.Direction.X * spell.ProjectileSpeed,
            Y = context.Direction.Y * spell.ProjectileSpeed,
            Z = context.Direction.Z * spell.ProjectileSpeed
        });
        /*
        projectile.AddComponent(new ProjectileComponent {
            SpellId = spell.Id,
            CasterEntityId = context.CasterEntityId,
            TargetEntityId = context.TargetEntityId,
            Context = context,
            Speed = spell.ProjectileSpeed,
            HitRadius = spell.ProjectileHitRadius,
            MaxLifetimeSeconds = spell.ProjectileLifetimeSeconds
        });*/
    }
}
