using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Packets;

/// <summary>
/// Load-test/debug packet: asks the server to spawn extra entities at runtime so
/// entity count can be ramped up without restarting the server.
/// </summary>
public sealed class SpawnEntitiesPacket : IPacket {
    public PacketType PacketType => PacketType.SpawnEntities;

    public string DefinitionId { get; set; } = "crowd";
    public int Count { get; set; }

    public void Serialize(NetDataWriter writer) {
        writer.Put(DefinitionId);
        writer.Put(Count);
    }

    public void Deserialize(NetDataReader reader) {
        DefinitionId = reader.GetString();
        Count = reader.GetInt();
    }
}
