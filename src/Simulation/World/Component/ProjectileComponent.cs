using System.Numerics;
using Yggdrasilnet.Network.Enums;

namespace Yggdrasilnet.Server.Simulation.World.Component;

public sealed class ProjectileComponent : Network.Packet.Snapshot.Components.ProjectileComponent, IComponent {
    public string SpellDefinitionId { get => SpellId; set => SpellId = value; }

    public Content.Spell.SpellCastContext? Context { get; set; }

    public Vector3 Direction { get; set; }

    public float ElapsedSeconds { get; set; }
    public float MaxLifetimeSeconds { get; set; }
    public float Speed { get; set; }
    public float HitRadius { get; set; } = 0.6f;

    public float AimHeight { get; set; } = 1f;

    public CollisionLayer TargetLayer { get; set; }

    public int RemainingHits { get; set; } = 1;

    public HashSet<int> AlreadyHit { get; } = [];

    public float ChainRadius { get; set; }
    public int BouncesRemaining { get; set; }
    public int BouncesDone { get; set; }
    public float ChainDamageMultiplier { get; set; } = 1f;

    public Entity? HomingTarget { get; set; }
}