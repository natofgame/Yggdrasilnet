using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using Serilog;
using Yggdrasilnet.Server.Simulation;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Server;

public sealed class NetServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly PacketRegistry _packetRegistry = new();
    private readonly ConcurrentQueue<ISimulationEvent> _simulationEvents;
    private readonly NetDataWriter _sendWriter = new();
    private long _sendCalls;
    private long _sendBytes;
    private long _sendSerializeTicks;
    private long _sendSocketTicks;
    private long _receiveCalls;
    private long _receiveBytes;
    private long _receiveDecodeTicks;

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
            _sendCalls++;
            _sendBytes += _sendWriter.Length;
            _sendSerializeTicks += afterSerialize - serializeStart;
            _sendSocketTicks += afterSend - afterSerialize;
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

        _receiveCalls++;
        _receiveBytes += packetBytes;
        _receiveDecodeTicks += decodeStop - decodeStart;

        reader.Recycle();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey("Yggdrasilnet");
    }

    public NetIoMetricsSnapshot CollectAndResetMetrics() {
        var sendCalls = _sendCalls;
        var sendBytes = _sendBytes;
        var sendSerializeTicks = _sendSerializeTicks;
        var sendSocketTicks = _sendSocketTicks;
        var receiveCalls = _receiveCalls;
        var receiveBytes = _receiveBytes;
        var receiveDecodeTicks = _receiveDecodeTicks;

        _sendCalls = 0;
        _sendBytes = 0;
        _sendSerializeTicks = 0;
        _sendSocketTicks = 0;
        _receiveCalls = 0;
        _receiveBytes = 0;
        _receiveDecodeTicks = 0;

        var ticksToMilliseconds = 1000d / Stopwatch.Frequency;
        return new NetIoMetricsSnapshot(
            sendCalls,
            sendBytes,
            sendSerializeTicks * ticksToMilliseconds,
            sendSocketTicks * ticksToMilliseconds,
            receiveCalls,
            receiveBytes,
            receiveDecodeTicks * ticksToMilliseconds
        );
    }
}

public readonly record struct NetIoMetricsSnapshot(
    long SendCalls,
    long SendBytes,
    double SendSerializeMs,
    double SendSocketMs,
    long ReceiveCalls,
    long ReceiveBytes,
    double ReceiveDecodeMs
);
