using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using Serilog;
using Yggdrasilnet.Server.Simulation;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Server.Services;

public sealed class NetServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly PacketRegistry _packetRegistry = new();
    private readonly ConcurrentQueue<ISimulationEvent> _simulationEvents;
    private readonly NetDataWriter _sendWriter = new();
    private readonly NetIoMetricsTracker _metrics = new();

    public NetServer(ConcurrentQueue<ISimulationEvent> worldEvents) {
        _simulationEvents = worldEvents;
        _netManager = new NetManager(this) {
            AutoRecycle = true,
        };
    }

    public void Start(int port) {
        if (!_netManager.Start(port)) {
            throw new InvalidOperationException($"Unable to bind UDP server on port {port}.");
        }
        Log.Information("Server listening on port {Port}", port);
    }

    public void Stop() {
        _netManager.Stop();
    }

    public void Poll() {
        _netManager.PollEvents();
    }

    public void Send<T>(NetPeer peer, T packet, DeliveryMethod method = DeliveryMethod.ReliableOrdered) where T : IPacket {
        lock (_sendWriter) {
            var serializeStart = Stopwatch.GetTimestamp();
            _sendWriter.Reset();
            _packetRegistry.Write(_sendWriter, packet);
            var afterSerialize = Stopwatch.GetTimestamp();
            peer.Send(_sendWriter, method);
            var afterSend = Stopwatch.GetTimestamp();
            _metrics.RecordSend(_sendWriter.Length, afterSerialize - serializeStart, afterSend - afterSerialize);
        }
    }

    public void OnPeerConnected(NetPeer peer) {
        _simulationEvents.Enqueue(new PeerConnectedEvent(peer));
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        _simulationEvents.Enqueue(new PeerDisconnectedEvent(peer, disconnectInfo));
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
        Log.Warning("Network error from {EndPoint}: {Error}", endPoint, socketError);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        var packetBytes = reader.AvailableBytes;
        var decodeStart = Stopwatch.GetTimestamp();
        if (_packetRegistry.TryRead(reader, out var packet)) {
            _simulationEvents.Enqueue(new PacketReceivedEvent(peer, packet));
        } else {
            Log.Warning("Received malformed packet from {EndPoint} ({Bytes} bytes)", peer.Address, reader.AvailableBytes);
        }
        var decodeStop = Stopwatch.GetTimestamp();

        _metrics.RecordReceive(packetBytes, decodeStop - decodeStart);

        reader.Recycle();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey("Yggdrasilnet");
    }

    public NetIoMetricsSnapshot CollectAndResetMetrics() => _metrics.CollectAndReset();
}
