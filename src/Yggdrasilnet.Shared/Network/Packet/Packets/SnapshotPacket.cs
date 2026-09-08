using LiteNetLib.Utils;
using Yggdrasilnet.Shared.Network.Packet.Snapshot;

namespace Yggdrasilnet.Shared.Network.Packet.Packets;

public sealed class SnapshotPacket : IPacket {
    public PacketType PacketType => PacketType.Snapshot;
    public List<EntitySnapshot> Entities { get; set; } = new();

    public void Serialize(NetDataWriter writer) {
        writer.Put((ushort)Entities.Count);
        foreach (var entity in Entities) {
            entity.WriteTo(writer);
        }
    }

    public void Deserialize(NetDataReader reader) {
        Entities.Clear();

        var count = reader.GetUShort();
        for (var i = 0; i < count; i++) {
            Entities.Add(EntitySnapshot.ReadFrom(reader, NetworkedComponentRegistry.Default));
        }
    }
}