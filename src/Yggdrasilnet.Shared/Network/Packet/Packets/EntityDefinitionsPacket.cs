using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Packets;

public sealed class EntityDefinitionsPacket : IPacket {
    public PacketType PacketType => PacketType.EntityDefinitions;

    public List<EntityDefinitionEntry> Definitions { get; set; } = new();

    public void Serialize(NetDataWriter writer) {
        writer.Put((ushort)Definitions.Count);
        foreach (var entry in Definitions) {
            writer.Put(entry.Id);
            writer.Put(entry.DefinitionId);
        }
    }

    public void Deserialize(NetDataReader reader) {
        var count = reader.GetUShort();
        Definitions = new List<EntityDefinitionEntry>(count);
        for (var i = 0; i < count; i++) {
            var id = reader.GetByte();
            var definitionId = reader.GetString();
            Definitions.Add(new EntityDefinitionEntry(id, definitionId));
        }
    }
}

public readonly record struct EntityDefinitionEntry(byte Id, string DefinitionId);
