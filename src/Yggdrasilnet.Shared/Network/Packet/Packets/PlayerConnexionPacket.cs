using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Packets;

public sealed class PlayerConnexionPacket : IPacket {
    public PacketType PacketType => PacketType.PlayerConnexion;

    public int PlayerId { get; set; }
    public bool IsOwner { get; set; }

    public void Serialize(NetDataWriter writer) {
        writer.Put(PlayerId);
        writer.Put(IsOwner);
    }

    public void Deserialize(NetDataReader reader) {
        PlayerId = reader.GetInt();
        IsOwner = reader.GetBool();
    }
}
