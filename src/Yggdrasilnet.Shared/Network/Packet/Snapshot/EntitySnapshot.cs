using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Snapshot;

public sealed class EntitySnapshot {
    public int EntityId { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public List<INetworkedComponent> Components { get; set; } = new();
    
    public uint LastInputSequence { get; set; }

    public void WriteTo(NetDataWriter writer) {
        writer.Put(EntityId);
        writer.Put(PositionX);
        writer.Put(PositionY);
        writer.Put(PositionZ);
        writer.Put(LastInputSequence);

        writer.Put((ushort)Components.Count);
        foreach (var component in Components) {
            writer.Put((byte)component.Type);
            component.Serialize(writer);
        }
    }

    public static EntitySnapshot ReadFrom(NetDataReader reader, NetworkedComponentRegistry registry) {
        var snapshot = new EntitySnapshot {
            EntityId = reader.GetInt(),
            PositionX = reader.GetFloat(),
            PositionY = reader.GetFloat(),
            PositionZ = reader.GetFloat(),
            LastInputSequence = reader.GetUInt(),
        };

        var count = reader.GetUShort();
        for (var i = 0; i < count; i++) {
            var typeByte = reader.GetByte();
            if (!Enum.IsDefined(typeof(NetworkedComponentType), typeByte)) {
                continue;
            }

            var type = (NetworkedComponentType)typeByte;
            if (!registry.TryCreate(type, out var component)) {
                continue;
            }

            component.Deserialize(reader);
            snapshot.Components.Add(component);
        }

        return snapshot;
    }
}
