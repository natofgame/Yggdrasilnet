using System.Numerics;
using Yggdrasilnet.Network.Enums;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;

public record SpellTargeting(float Range, Vector3 HitBoxMin, Vector3 HitBoxMax, CollisionLayer TargetLayer, 
    Vector3? CardinalHitBoxMin = null,
    Vector3? CardinalHitBoxMax = null);