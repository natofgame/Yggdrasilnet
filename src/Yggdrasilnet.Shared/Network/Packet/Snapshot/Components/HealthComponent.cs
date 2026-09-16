using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Snapshot.Components;

public class HealthComponent : INetworkedComponent {
    public NetworkedComponentType Type => NetworkedComponentType.Health;

    public float Current { get; set; }
    public float Max { get; set; }

    public void Serialize(NetDataWriter writer) {
        writer.Put(Current);
        writer.Put(Max);
    }

    public void Deserialize(NetDataReader reader) {
        Current = reader.GetFloat();
        Max = reader.GetFloat();
    }
}
