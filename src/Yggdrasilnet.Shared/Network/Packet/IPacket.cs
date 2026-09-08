using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet;

public interface IPacket : INetSerializable {
    PacketType PacketType { get; }
}