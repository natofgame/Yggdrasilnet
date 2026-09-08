using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Serilog;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.FakeClient;

public sealed class FakeClientListener : INetEventListener {
    private readonly PacketRegistry _packetRegistry = new();

    public NetPeer? Server { get; private set; }

    public void OnPeerConnected(NetPeer peer) {
        Server = peer;
        Log.Information("Connected to server {EndPoint} (peerId={PeerId})", peer.Address, peer.Id);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        Server = null;
        Log.Information("Disconnected from server ({Reason})", disconnectInfo.Reason);
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
        Log.Warning("Network error from {EndPoint}: {Error}", endPoint, socketError);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        if (_packetRegistry.TryRead(reader, out var packet)) {
            Log.Information("Received {PacketType}: {@Packet}", packet.PacketType, packet);
        } else {
            Log.Warning("Received malformed packet ({Bytes} bytes)", reader.AvailableBytes);
        }

        reader.Recycle();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request) { }
}
