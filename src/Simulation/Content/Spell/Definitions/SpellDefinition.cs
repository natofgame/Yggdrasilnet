using System.Numerics;
using Yggdrasilnet.Gameplay.Enums;
using Yggdrasilnet.Network.Enums;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions.Projectiles;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;

public sealed class SpellDefinition : IDefinition {
    public string Id { get; set; } = string.Empty;
    public SpellType Type { get; set; }
    public SpellTiming Timing { get; set; } = new(0f, 0f, 0f, 0f);
    public SpellTargeting Targeting { get; set; } = new(0f,  Vector3.Zero, Vector3.Zero, CollisionLayer.All);

    public List<SpellEffectDefinition> Effects { get; set; } = [];

    public bool SkipReturnPhase { get; set; } = false;
    
    public ProjectileDefinition? Projectile { get; set; }
}
