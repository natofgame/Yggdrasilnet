using LiteNetLib;
using LiteNetLib.Utils;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Shared.Network;

public enum PacketProcessResult {
    Malformed,
    NoHandler,
    Handled,
}

public sealed class PacketPipeline {
    private readonly PacketDispatcher _dispatcher = new();
    private readonly PacketRegistry _registry = new();

    public void Register<T>(PacketType type, IPacketHandler<T> handler) where T : IPacket {
        _dispatcher.Register(type, handler);
    }

    public PacketProcessResult Process(NetPeer peer, NetDataReader reader) {
        if (!_registry.TryRead(reader, out var packet)) {
            return PacketProcessResult.Malformed;
        }

        return _dispatcher.Dispatch(peer, packet)
            ? PacketProcessResult.Handled
            : PacketProcessResult.NoHandler;
    }

    public void Send<T>(NetPeer peer, T packet, DeliveryMethod method = DeliveryMethod.ReliableOrdered) where T : IPacket {
        var writer = new NetDataWriter();
        _registry.Write(writer, packet);
        peer.Send(writer, method);
    }
}
