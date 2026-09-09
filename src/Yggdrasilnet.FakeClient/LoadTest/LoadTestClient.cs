using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using Yggdrasilnet.Shared.Network.Packet;
using Yggdrasilnet.Shared.Network.Packet.Packets;
using Yggdrasilnet.Shared.Network.Packet.Snapshot;

namespace Yggdrasilnet.FakeClient.LoadTest;

public sealed class LoadTestClient : INetEventListener, IDisposable {
    private const int InputIntervalMs = 100;
    private const int MaxLatencySamples = 50;
    private const int MaxPendingSequences = 200;

    private readonly NetManager _netManager;
    private readonly PacketRegistry _packetRegistry = new();
    private readonly ConcurrentDictionary<uint, long> _pendingSendTimestampsMs = new();
    private readonly ConcurrentQueue<double> _recentLatenciesMs = new();

    private uint _sequence;
    private long _lastInputSentMs;
    private int _entityId = -1;

    public NetPeer? Peer { get; private set; }
    public bool WasConnected { get; private set; }
    public bool Faulted { get; private set; }
    public StatsPacket? LastStats { get; private set; }

    public LoadTestClient(string host, int port) {
        _netManager = new NetManager(this) { AutoRecycle = true };
        _netManager.Start();
        _netManager.Connect(host, port, "Yggdrasilnet");
    }

    public void Poll() {
        _netManager.PollEvents();
        MaybeSendInput();
    }

    public double? AverageLatencyMs() => _recentLatenciesMs.IsEmpty ? null : _recentLatenciesMs.Average();
    public double? MaxLatencyMs() => _recentLatenciesMs.IsEmpty ? null : _recentLatenciesMs.Max();

    public void RequestSpawnEntities(string definitionId, int count) {
        if (Peer is not { } peer) {
            return;
        }

        var writer = new NetDataWriter();
        _packetRegistry.Write(writer, new SpawnEntitiesPacket { DefinitionId = definitionId, Count = count });
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void MaybeSendInput() {
        if (Peer is not { } peer) {
            return;
        }

        var now = Environment.TickCount64;
        if (now - _lastInputSentMs < InputIntervalMs) {
            return;
        }
        _lastInputSentMs = now;

        var sequence = ++_sequence;
        _pendingSendTimestampsMs[sequence] = now;
        TrimPending();
        
        var angle = now / 1500.0;
        var writer = new NetDataWriter();
        _packetRegistry.Write(writer, new InputPacket {
            Sequence = sequence,
            MoveX = (float)Math.Sin(angle),
            MoveZ = (float)Math.Cos(angle),
            DeltaTime = InputIntervalMs / 1000f,
        });
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void TrimPending() {
        if (_pendingSendTimestampsMs.Count <= MaxPendingSequences) {
            return;
        }

        var oldestAllowed = _sequence - MaxPendingSequences;
        foreach (var sequence in _pendingSendTimestampsMs.Keys) {
            if (sequence < oldestAllowed) {
                _pendingSendTimestampsMs.TryRemove(sequence, out _);
            }
        }
    }

    private void RecordLatency(double ms) {
        _recentLatenciesMs.Enqueue(ms);
        while (_recentLatenciesMs.Count > MaxLatencySamples) {
            _recentLatenciesMs.TryDequeue(out _);
        }
    }

    public void OnPeerConnected(NetPeer peer) {
        Peer = peer;
        WasConnected = true;

        var writer = new NetDataWriter();
        _packetRegistry.Write(writer, new PlayerConnexionPacket { PlayerId = peer.Id, IsOwner = true });
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        if (WasConnected) {
            Faulted = true;
        }
        Peer = null;
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        if (_packetRegistry.TryRead(reader, out var packet)) {
            switch (packet) {
                case PlayerConnexionPacket connexion:
                    _entityId = connexion.EntityId;
                    break;
                case SnapshotPacket snapshot:
                    HandleSnapshot(snapshot);
                    break;
                case SnapshotChunkPacket snapshotChunk:
                    HandleSnapshotEntities(snapshotChunk.Entities);
                    break;
                case StatsPacket stats:
                    LastStats = stats;
                    break;
            }
        }

        reader.Recycle();
    }

    private void HandleSnapshot(SnapshotPacket snapshot) {
        HandleSnapshotEntities(snapshot.Entities);
    }

    private void HandleSnapshotEntities(IReadOnlyList<EntitySnapshot> entities) {
        if (_entityId == -1) {
            return;
        }

        foreach (var entity in entities) {
            if (entity.EntityId != _entityId) {
                continue;
            }

            if (_pendingSendTimestampsMs.TryRemove(entity.LastInputSequence, out var sentAtMs)) {
                RecordLatency(Environment.TickCount64 - sentAtMs);
            }
            break;
        }
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request) { }

    public void Dispose() {
        _netManager.Stop();
    }
}
