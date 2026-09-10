using LiteNetLib.Utils;
using Yggdrasilnet.Shared.Network.Packet.Snapshot;

namespace Yggdrasilnet.Shared.Network.Packet.Packets;

public sealed class SnapshotChunkPacket : IPacket {
    public PacketType PacketType => PacketType.SnapshotChunk;

    public uint FrameId { get; set; }
    public ushort ChunkIndex { get; set; }
    public bool IsLastChunk { get; set; }
    public List<EntitySnapshot> Entities { get; set; } = new();

    public void Serialize(NetDataWriter writer) {
        writer.Put(FrameId);
        writer.Put(ChunkIndex);
        writer.Put(IsLastChunk);

        writer.Put((ushort)Entities.Count);
        foreach (var entity in Entities) {
            entity.WriteTo(writer);
        }
    }

    public void Deserialize(NetDataReader reader) {
        FrameId = reader.GetUInt();
        ChunkIndex = reader.GetUShort();
        IsLastChunk = reader.GetBool();

        Entities.Clear();
        var count = reader.GetUShort();
        for (var i = 0; i < count; i++) {
            Entities.Add(EntitySnapshot.ReadFrom(reader, NetworkedComponentRegistry.Default));
        }
    }
}
