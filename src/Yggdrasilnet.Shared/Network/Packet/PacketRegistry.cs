using LiteNetLib.Utils;
using Yggdrasilnet.Shared.Network.Packet.Packets;

namespace Yggdrasilnet.Shared.Network.Packet;

public sealed class PacketRegistry {
    private readonly Dictionary<PacketType, Func<IPacket>> _factories = new();

    public PacketRegistry() {
        Register(PacketType.PlayerConnexion, () => new PlayerConnexionPacket());
        Register(PacketType.Snapshot, () => new SnapshotPacket());
    }

    public void Register(PacketType type, Func<IPacket> factory) {
        _factories[type] = factory;
    }

    public bool TryCreate(PacketType type, out IPacket packet) {
        if (_factories.TryGetValue(type, out var factory)) {
            packet = factory();
            return true;
        }

        packet = null!;
        return false;
    }

    public bool TryRead(NetDataReader reader, out IPacket packet) {
        var typeByte = reader.GetByte();

        if (!Enum.IsDefined(typeof(PacketType), typeByte)) {
            packet = null!;
            return false;
        }

        var type = (PacketType)typeByte;
        if (!TryCreate(type, out packet)) {
            return false;
        }

        packet.Deserialize(reader);
        return true;
    }

    public void Write(NetDataWriter writer, IPacket packet) {
        writer.Put((byte)packet.PacketType);
        packet.Serialize(writer);
    }
}
