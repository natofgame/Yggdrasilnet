using LiteNetLib;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Shared.Network;

public interface IPacketHandler<T> where T : IPacket {
    public void Handle(NetPeer peer, T packet);
}