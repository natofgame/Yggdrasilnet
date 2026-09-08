namespace Yggdrasilnet.Server.Simulation.World.Component;

public sealed class HealthComponent : IComponent {
    public float Max { get; set; }
    public float Current { get; set; }
}
