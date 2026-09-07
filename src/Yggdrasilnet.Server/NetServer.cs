using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Serilog;
using Yggdrasilnet.Server.Handlers;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Server;

public sealed class NetServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly int _tickRate;
    private readonly PacketPipeline _packetPipeline = new();

    public NetServer(int tickRate = 30) {
        _tickRate = tickRate;
        _netManager = new NetManager(this) {
            AutoRecycle = true,
        };

        RegisterHandlers();
    }

    private void RegisterHandlers() {
        _packetPipeline.Register(PacketType.PlayerConnexion, new PlayerConnexionHandler());
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
        var result = _packetPipeline.Process(peer, reader);

        switch (result) {
            case PacketProcessResult.Malformed:
                Log.Warning("Received malformed packet from {EndPoint} ({Bytes} bytes)", peer.Address, reader.AvailableBytes);
                break;
            case PacketProcessResult.NoHandler:
                Log.Warning("No handler registered for packet from {EndPoint}", peer.Address);
                break;
            case PacketProcessResult.Handled:
                break;
        }

        reader.Recycle();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey("Yggdrasilnet");
    }
}
