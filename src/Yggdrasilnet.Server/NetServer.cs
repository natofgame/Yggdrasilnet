using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Serilog;

namespace Yggdrasilnet.Server;

/// <summary>
/// Minimal LiteNetLib server wrapper. Owns the update loop and dispatches
/// connection lifecycle events. Packet handling will be plugged in later.
/// </summary>
public sealed class NetServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly int _tickRate;

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
        // TODO: forward to a packet dispatcher once the shared packet protocol exists.
        reader.Recycle();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey("Yggdrasilnet");
    }
}
