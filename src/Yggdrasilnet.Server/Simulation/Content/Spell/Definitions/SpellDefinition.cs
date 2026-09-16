using System.Numerics;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;
using Yggdrasilnet.Shared.Maths;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;

public sealed class SpellDefinition : IDefinition {
    public string Id { get; set; } = string.Empty;
    public SpellType Type { get; set; }
    public List<SpellEffectDefinition> Effects { get; set; } = [];

    public float AnticipationSeconds { get; set; }
    public float StrikeSeconds { get; set; }
    public float ImpactSeconds { get; set; }
    public float ReturnSeconds { get; set; }

    public bool SkipReturnPhase { get; set; } = false;

    public float Damage { get; set; }
    public float Range { get; set; }
    
    public Vector3 HitBoxMin { get; set; }
    public Vector3 HitBoxMax { get; set; }
    public BoundingBoxes HitBoxes =>  new BoundingBoxes(HitBoxMin, HitBoxMax);

    public string ProjectileEntityId { get; set; } = string.Empty;
    public float ProjectileSpeed { get; set; }
    public float ProjectileLifetimeSeconds { get; set; } = 5f;
    public float ProjectileHitRadius { get; set; } = 0.6f;
}
