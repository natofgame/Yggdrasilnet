using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared;

public interface IPacket : INetSerializable {
    PacketType PacketType { get; }
}