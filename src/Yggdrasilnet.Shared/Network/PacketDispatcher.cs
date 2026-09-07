using LiteNetLib;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Shared.Network;

public sealed class PacketDispatcher {
    private readonly Dictionary<PacketType, Action<NetPeer, IPacket>> _handlers = new();

    public void Register<T>(PacketType type, IPacketHandler<T> handler) where T : IPacket {
        _handlers[type] = (peer, packet) => handler.Handle(peer, (T)packet);
    }

    public bool Dispatch(NetPeer peer, IPacket packet) {
        if (!_handlers.TryGetValue(packet.PacketType, out var handle)) {
            return false;
        }

        handle(peer, packet);
        return true;
    }
}