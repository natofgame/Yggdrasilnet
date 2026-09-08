using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Snapshot.Components;

public class VelocityComponent : INetworkedComponent {
    public NetworkedComponentType Type => NetworkedComponentType.Velocity;

    public float X, Y, Z;

    public void Serialize(NetDataWriter writer) {
        writer.Put(X);
        writer.Put(Y);
        writer.Put(Z);
    }

    public void Deserialize(NetDataReader reader) {
        X = reader.GetFloat();
        Y = reader.GetFloat();
        Z = reader.GetFloat();
    }
}
