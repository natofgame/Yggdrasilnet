namespace Yggdrasilnet.Server.Simulation.Content.Spell.Definitions.Projectiles;

public sealed class ProjectileChainDefinition {
    public int MaxBounces { get; set; } = 3;

    public float Radius { get; set; } = 6f;

    public float DamageMultiplierPerBounce { get; set; } = 0.8f;
}