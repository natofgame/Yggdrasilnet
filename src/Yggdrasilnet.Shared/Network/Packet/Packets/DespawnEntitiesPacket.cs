using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Packets;

public sealed class DespawnEntitiesPacket : IPacket {
    public PacketType PacketType => PacketType.DespawnEntities;

    public List<int> EntityIds { get; set; } = new();

    public void Serialize(NetDataWriter writer) {
        writer.Put(checked((ushort)EntityIds.Count));
        foreach (var entityId in EntityIds) {
            writer.Put(entityId);
        }
    }

    public void Deserialize(NetDataReader reader) {
        EntityIds.Clear();
        var count = reader.GetUShort();
        for (var i = 0; i < count; i++) {
            EntityIds.Add(reader.GetInt());
        }
    }
}
