namespace Yggdrasilnet.Shared.Network.Packet.Snapshot;

public sealed class NetworkedComponentRegistry {
    private static readonly Lazy<NetworkedComponentRegistry> DefaultInstance = new(CreateDefault);
    
    public static NetworkedComponentRegistry Default => DefaultInstance.Value;

    private static NetworkedComponentRegistry CreateDefault() {
        var registry = new NetworkedComponentRegistry();
        registry.Register(NetworkedComponentType.Velocity, () => new Components.VelocityComponent());
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