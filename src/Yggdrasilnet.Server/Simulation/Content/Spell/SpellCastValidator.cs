using System.Numerics;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content.Spell;

public static class SpellCastValidator {
    
    // TODO: Moove
    public static bool IsLiving(World.Entity entity) =>
        entity.TryGetComponent<HealthComponent>(out var health) && float.IsFinite(health.Current) && health.Current > 0f;
    
    //TODO: Reimplente self spell
    public static bool TryCreate(World.World world, World.Entity caster, SpellDefinition spell, out SpellCastContext context) {
        context = null!;
        if (!IsLiving(caster) || !Enum.IsDefined(spell.Type)) {
            return false;
        }

        var direction = !caster.TryGetComponent<DirectionComponent>(out var directionComponent) ? new Vector3(directionComponent.X,  0, directionComponent.Z) : Vector3.Zero;
        context = new SpellCastContext(caster.Id, spell.Id, spell.Type,
            direction, caster.Position);
        return true;
    }
}
