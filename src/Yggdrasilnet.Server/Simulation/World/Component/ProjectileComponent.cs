namespace Yggdrasilnet.Server.Simulation.World.Component;

public sealed class ProjectileComponent : Shared.Network.Packet.Snapshot.Components.ProjectileComponent, IComponent {
    public string SpellDefinitionId { get => SpellId; set => SpellId = value; }
    public Content.Spell.SpellCastContext? Context { get; set; }
    public float ElapsedSeconds { get; set; }
    public float MaxLifetimeSeconds { get; set; }
    public float Speed { get; set; }
    public float HitRadius { get; set; } = 0.6f;
}
