using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Serilog;
using Yggdrasilnet.Server.Handlers;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Server;

public sealed class NetServer : INetEventListener {
    private readonly Queue<INetEvent> _netEvents = new();

    private readonly GameHandler _gameHandler;
    private readonly NetManager _netManager;
    private readonly int _tickRate;

    public PacketPipeline PacketPipeline { get; } = new();
    
    public long Tick { get; private set; }
    public float DeltaTime { get; private set; }
    
    public NetServer(int tickRate = 30) {
        _tickRate = tickRate;
        _netManager = new NetManager(this) {
            AutoRecycle = true,
        };

        _gameHandler = new GameHandler();
        PacketPipeline.Register(PacketType.PlayerConnexion, new PlayerConnexionHandler());
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
        _netEvents.Enqueue(new PeerConnectedEvent(peer));
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        _netEvents.Enqueue(new PeerDisconnectedEvent(peer, disconnectInfo));
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
        Log.Warning("Network error from {EndPoint}: {Error}", endPoint, socketError);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        var result = PacketPipeline.Process(peer, reader);

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

    public void OnUpdate(float deltaTime) {
        Tick++;
        DeltaTime = deltaTime;
        
        _netManager.PollEvents();
        ProcessNetEvents();
    }

    private void ProcessNetEvents() {
        while (_netEvents.Count > 0) {
            var netEvent = _netEvents.Dequeue();

            switch (netEvent) {
                case PeerConnectedEvent peerConnectedEvent:
                    _gameHandler.CreatePlayer(peerConnectedEvent.Peer);
                    break;
                case PeerDisconnectedEvent peerDisconnectedEvent:
                    _gameHandler.RemovePlayer(peerDisconnectedEvent.Peer, peerDisconnectedEvent.Info);
                    break;
            }
        }
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey("Yggdrasilnet");
    }
}
