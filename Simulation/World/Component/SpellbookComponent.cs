namespace Yggdrasilnet.Server.Simulation.World.Component;

public sealed class SpellbookComponent : IComponent {
    public List<string> Spells { get; set; } = [];
}
