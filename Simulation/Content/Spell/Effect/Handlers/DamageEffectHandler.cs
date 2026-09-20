

using System.Diagnostics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Effect.Handlers;

public class DamageEffectHandler : ISpellEffectHandler {
    public void Apply(World.World world, SpellCastContext context, World.Entity actor, float amount, float duration) {
        if (!actor.TryGetComponent<HealthComponent>(out var health)) {
            return;
        }
        
        health.Current = MathF.Max(0f, health.Current - amount);
        Console.WriteLine("Damage: " + actor.Id);
    }
}