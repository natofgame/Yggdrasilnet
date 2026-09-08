using LiteNetLib;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Shared.Network;

public interface IPacketHandler<TPacket, in TContext> where TPacket : IPacket {
    public void Handle(NetPeer peer, TPacket packet, TContext context);
}