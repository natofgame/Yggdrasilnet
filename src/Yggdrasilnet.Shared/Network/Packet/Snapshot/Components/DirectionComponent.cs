using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Snapshot.Components;

public class DirectionComponent : INetworkedComponent {
    public NetworkedComponentType Type => NetworkedComponentType.Direction;

    public float X, Z;

    public void Serialize(NetDataWriter writer) {
        writer.Put(X);
        writer.Put(Z);
    }

    public void Deserialize(NetDataReader reader) {
        X = reader.GetFloat();
        Z = reader.GetFloat();
    }
}