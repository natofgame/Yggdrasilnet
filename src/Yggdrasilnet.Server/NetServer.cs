using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Serilog;
using Yggdrasilnet.Shared;
using Yggdrasilnet.Shared.Packet;

namespace Yggdrasilnet.Server;

public sealed class NetServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly int _tickRate;
    private readonly PacketRegistry _packetRegistry = new();

    public NetServer(int tickRate = 30) {
        _tickRate = tickRate;
        _netManager = new NetManager(this) {
            AutoRecycle = true,
        };
    }

    public void Start(int port) {
        _netManager.Start(port);
        Log.Information("Server listening on port {Port} ({TickRate} tps)", port, _tickRate);
    }

    public void Stop() {
        _netManager.Stop();
    }

    public async Task RunAsync(CancellationToken cancellationToken) {
        var tickInterval = TimeSpan.FromSeconds(1.0 / _tickRate);
        while (!cancellationToken.IsCancellationRequested) {
            _netManager.PollEvents();
            await Task.Delay(tickInterval, cancellationToken).ContinueWith(_ => { });
        }
    }

    public void OnPeerConnected(NetPeer peer) {
        Log.Information("Peer connected: {EndPoint}", peer.Address);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        Log.Information("Peer disconnected: {EndPoint} ({Reason})", peer.Address, disconnectInfo.Reason);
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
        Log.Warning("Network error from {EndPoint}: {Error}", endPoint, socketError);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        if (!_packetRegistry.TryRead(reader, out var packet)) {
            Log.Warning("Received unknown/malformed packet from {EndPoint} ({Bytes} bytes)", peer.Address, reader.AvailableBytes);
            reader.Recycle();
            return;
        }

        Log.Debug("Received {PacketType} from {EndPoint}", packet.PacketType, peer.Address);
        // TODO: forward `packet` to a dispatcher/handler once game logic exists.
        reader.Recycle();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey("Yggdrasilnet");
    }
}
