namespace Yggdrasilnet.Server.Simulation.Content.Spell.Definitions.Projectiles;

public sealed class ProjectileDefinition {
    public float Speed { get; set; } = 12f;

    public float Lifetime { get; set; } = 2f;

    public float HitRadius { get; set; } = 0.4f;

    public int MaxHits { get; set; } = 1;

    public float SpawnForward { get; set; } = 0.3f;

    public float SpawnHeight { get; set; } = 1f;

    public ProjectileChainDefinition? Chain { get; set; }
}

