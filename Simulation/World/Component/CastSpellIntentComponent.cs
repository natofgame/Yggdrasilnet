namespace Yggdrasilnet.Server.Simulation.World.Component;

public sealed class CastSpellIntentComponent : IComponent {
    public string SpellDefinitionId { get; set; } = string.Empty;
    public byte SpellIndex { get; set; }
}
