using LiteNetLib;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Shared.Network;

public sealed class PacketDispatcher<TContext> {
    private readonly Dictionary<PacketType, Action<NetPeer, IPacket, TContext>> _handlers = new();

    public void Register<T>(PacketType type, IPacketHandler<T, TContext> handler) where T : IPacket {
        _handlers[type] = (peer, packet, context) => handler.Handle(peer, (T)packet, context);
    }

    public bool Dispatch(NetPeer peer, IPacket packet, TContext context) {
        if (!_handlers.TryGetValue(packet.PacketType, out var handle)) {
            return false;
        }

        handle(peer, packet, context);
        return true;
    }
}