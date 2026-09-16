namespace Yggdrasilnet.Server.Simulation.Content.Spell.Effect.Handlers;

public interface ISpellEffectHandler {
    void Apply(World.World world, SpellCastContext context, World.Entity actor, float amount, float duration);
}