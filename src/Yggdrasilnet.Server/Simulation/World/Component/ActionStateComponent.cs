using Yggdrasilnet.Shared.Network.Packet.Snapshot.Components;

namespace Yggdrasilnet.Server.Simulation.World.Component;

public sealed class ActionStateComponent : ActionComponent, IComponent {
    public string SpellDefinitionId { get => SpellId; set => SpellId = value; }
    public float PhaseTimeRemaining { get; set; }
    public float CurrentPhaseDuration { get; set; }
    public int CasterEntityId { get; set; }
    public Content.Spell.SpellCastContext? Context { get; set; }
}