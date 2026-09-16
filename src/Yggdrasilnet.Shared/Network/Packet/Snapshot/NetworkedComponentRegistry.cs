using Yggdrasilnet.Shared.Network.Packet.Snapshot.Components;

namespace Yggdrasilnet.Shared.Network.Packet.Snapshot;

public sealed class NetworkedComponentRegistry {
    private static readonly Lazy<NetworkedComponentRegistry> DefaultInstance = new(CreateDefault);
    
    public static NetworkedComponentRegistry Default => DefaultInstance.Value;

    private static NetworkedComponentRegistry CreateDefault() {
        var registry = new NetworkedComponentRegistry();
        registry.Register(NetworkedComponentType.Velocity, () => new Components.VelocityComponent());
        registry.Register(NetworkedComponentType.Action, () => new Components.ActionComponent());
        registry.Register(NetworkedComponentType.Health, () => new Components.HealthComponent());
        registry.Register(NetworkedComponentType.Projectile, () => new Components.ProjectileComponent());
        registry.Register(NetworkedComponentType.Collision, () => new CollisionComponent());
        return registry;
    }

    private readonly Dictionary<NetworkedComponentType, Func<INetworkedComponent>> _factories = new();

    public void Register(NetworkedComponentType type, Func<INetworkedComponent> factory) {
        _factories[type] = factory;
    }

    public bool TryCreate(NetworkedComponentType type, out INetworkedComponent component) {
        if (_factories.TryGetValue(type, out var factory)) {
            component = factory();
            return true;
        }

        component = null!;
        return false;
    }
}