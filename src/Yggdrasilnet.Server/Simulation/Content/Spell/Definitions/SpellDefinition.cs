using System.Numerics;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;
using Yggdrasilnet.Shared.Compatibility;
using Yggdrasilnet.Shared.Maths;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;

public sealed class SpellDefinition : IDefinition {
    public string Id { get; set; } = string.Empty;
    public SpellType Type { get; set; }
    public SpellTiming Timing { get; set; } = new(0f, 0f, 0f, 0f);
    public SpellTargeting Targeting { get; set; } = new(0f,  Vector3.Zero, Vector3.Zero, CollisionLayer.All);

    public List<SpellEffectDefinition> Effects { get; set; } = [];

    public bool SkipReturnPhase { get; set; } = false;
    
    public string ProjectileEntityId { get; set; } = string.Empty;
    public float ProjectileSpeed { get; set; }
    public float ProjectileLifetimeSeconds { get; set; } = 5f;
    public float ProjectileHitRadius { get; set; } = 0.6f;
}
