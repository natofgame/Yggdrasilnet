using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using LiteNetLib;
using LiteNetLib.Utils;
using Yggdrasilnet.Server.Simulation;
using Yggdrasilnet.Shared.Network.Packet;
using Yggdrasilnet.Shared.Network.Packet.Packets;
using Yggdrasilnet.Shared.Network.Packet.Snapshot;
using Yggdrasilnet.Shared.Network.Packet.Snapshot.Components;

namespace Yggdrasilnet.Server.Tests;

public sealed class SnapshotBroadcastServiceTests {
    private static readonly MethodInfo SendSnapshotChunks = typeof(SnapshotBroadcastService)
        .GetMethod("SendSnapshotChunks", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void ReusedChunkAndWriterPreserveWireBytesAcrossRecipientsAndFrames() {
        using var network = new LoopbackPeers();
        var server = new NetServer(new ConcurrentQueue<ISimulationEvent>());
        try {
            var service = new SnapshotBroadcastService(server, null!, 60);
            var metrics = new SnapshotBroadcastMetrics { Sessions = 2, SnapshotEntities = 718, Keyframe = true };
            var sends = new[] {
                NewSend(network, 0, 1, DeliveryMethod.ReliableOrdered, Entities(220, 1000)),
                NewSend(network, 1, 1, DeliveryMethod.ReliableOrdered, Entities(220, 2000)),
                NewSend(network, 0, 2, DeliveryMethod.Unreliable, Entities(57, 3000)),
                NewSend(network, 1, 2, DeliveryMethod.Unreliable, Entities(220, 4000)),
                NewSend(network, 0, 3, DeliveryMethod.ReliableOrdered, Entities(1, 5000))
            };
            var retained = sends.Select(send => SerializeEntities(send.Entities)).ToArray();
            var expectedChunks = 0;
            foreach (var send in sends) {
                metrics = Send(service, network.Peers[send.Recipient], send.Entities, send.FrameId,
                    send.Delivery, metrics);
                expectedChunks += send.Bytes.Length;
                Assert.Equal(expectedChunks, metrics.ChunksSent);
                Assert.Equal(0, metrics.DroppedOversizedEntities);
            }

            // Do not dispatch receives until every send has reused the packet and serialization buffer.
            network.WaitUntil(() => network.Received.Sum(packets => packets.Count) >= expectedChunks);
            Assert.Equal(expectedChunks, network.Received.Sum(packets => packets.Count));
            foreach (var send in sends) {
                AssertWire(network, send);
            }
            for (var i = 0; i < sends.Length; i++) {
                Assert.Equal(retained[i], SerializeEntities(sends[i].Entities));
                Assert.All(sends[i].Entities, entity => Assert.Equal(35, entity.EstimatedBytes));
            }
            Assert.Equal(220, sends[0].Entities.Count);
            Assert.Equal(220, sends[1].Entities.Count);
            Assert.Equal(220, sends[3].Entities.Count);
            Assert.Equal(2, metrics.Sessions);
            Assert.Equal(718, metrics.SnapshotEntities);
            Assert.True(metrics.Keyframe);
            AssertIoMetrics(server, sends);
        } finally {
            server.Stop();
        }
    }

    [Theory]
    [InlineData(DeliveryMethod.ReliableOrdered)]
    [InlineData(DeliveryMethod.Unreliable)]
    public void OversizedAndEmptySendsPreserveAccountingAndResetChunkFlags(DeliveryMethod delivery) {
        using var network = new LoopbackPeers();
        var server = new NetServer(new ConcurrentQueue<ISimulationEvent>());
        try {
            var service = new SnapshotBroadcastService(server, null!, 60);
            var metrics = new SnapshotBroadcastMetrics {
                ChunksSent = 7, DroppedOversizedEntities = 11, Sessions = 2, SnapshotEntities = 99
            };
            var warmup = NewSend(network, 0, 10, delivery, Entities(1, 100));
            var accepted = Entities(220, 1000);
            var mixed = new List<EntitySnapshot> { Oversized(9000) };
            mixed.AddRange(accepted.Take(100));
            mixed.Add(Oversized(9001));
            mixed.AddRange(accepted.Skip(100));
            mixed.Add(Oversized(9002));
            var retained = SerializeEntities(mixed);
            var populated = NewSend(network, 0, 11, delivery, accepted);
            var next = NewSend(network, 0, 14, delivery, Entities(60, 2000));
            var sends = new[] { warmup, populated, next };

            metrics = Send(service, network.Peers[0], warmup.Entities, 10, delivery, metrics);
            Assert.Equal(8, metrics.ChunksSent);
            Assert.Equal(11, metrics.DroppedOversizedEntities);
            metrics = Send(service, network.Peers[0], mixed, 11, delivery, metrics);
            Assert.Equal(8 + populated.Bytes.Length, metrics.ChunksSent);
            Assert.Equal(14, metrics.DroppedOversizedEntities);

            var chunksBeforeSkipped = metrics.ChunksSent;
            var ioBeforeSkipped = server.CollectAndResetMetrics();
            metrics = Send(service, network.Peers[0], [Oversized(9003), Oversized(9004)],
                12, delivery, metrics);
            Assert.Equal(chunksBeforeSkipped, metrics.ChunksSent);
            Assert.Equal(16, metrics.DroppedOversizedEntities);
            metrics = Send(service, network.Peers[0], [], 13, delivery, metrics);
            Assert.Equal(chunksBeforeSkipped, metrics.ChunksSent);
            Assert.Equal(16, metrics.DroppedOversizedEntities);
            var skippedIo = server.CollectAndResetMetrics();
            Assert.Equal(0L, skippedIo.SendCalls);
            Assert.Equal(0L, skippedIo.SendBytes);

            metrics = Send(service, network.Peers[0], next.Entities, 14, delivery, metrics);
            Assert.Equal(chunksBeforeSkipped + next.Bytes.Length, metrics.ChunksSent);
            Assert.Equal(16, metrics.DroppedOversizedEntities);
            Assert.Equal(2, metrics.Sessions);
            Assert.Equal(99, metrics.SnapshotEntities);
            Assert.False(metrics.Keyframe);
            Assert.Equal(retained, SerializeEntities(mixed));
            Assert.Equal(223, mixed.Count);
            Assert.Equal(220, accepted.Count);

            var expectedChunks = sends.Sum(send => send.Bytes.Length);
            network.WaitUntil(() => network.Received[0].Count >= expectedChunks);
            Assert.Equal(expectedChunks, network.Received[0].Count);
            Assert.Empty(network.Received[1]);
            foreach (var send in sends) {
                AssertWire(network, send);
            }
            var ioAfterSkipped = server.CollectAndResetMetrics();
            Assert.Equal((long)expectedChunks, ioBeforeSkipped.SendCalls + ioAfterSkipped.SendCalls);
            Assert.Equal(sends.Sum(send => send.Bytes.Sum(bytes => (long)bytes.Length)),
                ioBeforeSkipped.SendBytes + ioAfterSkipped.SendBytes);
        } finally {
            server.Stop();
        }
    }

    private static SnapshotBroadcastMetrics Send(SnapshotBroadcastService service, NetPeer peer,
        List<EntitySnapshot> entities, uint frameId, DeliveryMethod delivery, SnapshotBroadcastMetrics metrics) {
        object[] arguments = [peer, entities, frameId, delivery == DeliveryMethod.ReliableOrdered, metrics];
        SendSnapshotChunks.Invoke(service, arguments);
        return (SnapshotBroadcastMetrics)arguments[4];
    }

    private static List<EntitySnapshot> Entities(int count, int offset) {
        return Enumerable.Range(0, count).Select(i => new EntitySnapshot {
            EntityId = offset + i,
            PositionX = offset + i + 0.25f,
            PositionY = -offset - i - 0.5f,
            PositionZ = i * 2 + 0.75f,
            LastInputSequence = (uint)(offset * 10 + i),
            EstimatedBytes = 35,
            Components = [new VelocityComponent { X = offset + i, Y = -i - 0.25f, Z = offset * 2 + i }]
        }).ToList();
    }

    private static EntitySnapshot Oversized(int id) {
        return new EntitySnapshot {
            EntityId = id,
            EstimatedBytes = 22 + 100 * 13,
            Components = Enumerable.Range(0, 100)
                .Select(i => (INetworkedComponent)new VelocityComponent { X = i, Y = -i, Z = id }).ToList()
        };
    }

    private static ExpectedSend NewSend(LoopbackPeers network, int recipient, uint frameId,
        DeliveryMethod delivery, List<EntitySnapshot> entities) {
        var target = Math.Min(1000, network.Peers[recipient].GetMaxSinglePacketSize(delivery) - 96);
        var entitiesPerChunk = (target - 10) / 35;
        Assert.True(entitiesPerChunk > 0);
        var chunks = entities.Chunk(entitiesPerChunk).ToArray();
        var bytes = chunks.Select((chunk, index) => Serialize(new SnapshotChunkPacket {
            FrameId = frameId,
            ChunkIndex = (ushort)index,
            IsLastChunk = index == chunks.Length - 1,
            Entities = chunk.ToList()
        })).ToArray();
        return new ExpectedSend(recipient, frameId, delivery, entities, target, entitiesPerChunk, bytes);
    }

    private static void AssertWire(LoopbackPeers network, ExpectedSend expected) {
        var received = network.Received[expected.Recipient]
            .Where(packet => packet.Packet.FrameId == expected.FrameId)
            .OrderBy(packet => packet.Packet.ChunkIndex).ToArray();
        Assert.Equal(expected.Bytes.Length, received.Length);
        for (var i = 0; i < received.Length; i++) {
            var actual = received[i];
            Assert.Equal(expected.Delivery, actual.Delivery);
            Assert.Equal(expected.FrameId, actual.Packet.FrameId);
            Assert.Equal((ushort)i, actual.Packet.ChunkIndex);
            Assert.Equal(i == received.Length - 1, actual.Packet.IsLastChunk);
            Assert.Equal(expected.Bytes[i], actual.Bytes);
            Assert.InRange(actual.Bytes.Length, 11, expected.TargetBytes);
            Assert.Equal(10 + actual.Packet.Entities.Count * 35, actual.Bytes.Length);
            Assert.NotEmpty(actual.Packet.Entities);
            if (i < received.Length - 1) {
                Assert.Equal(expected.EntitiesPerChunk, actual.Packet.Entities.Count);
                Assert.True(actual.Bytes.Length + 35 > expected.TargetBytes);
            }
        }
        Assert.Equal(expected.Entities.Select(entity => entity.EntityId),
            received.SelectMany(packet => packet.Packet.Entities).Select(entity => entity.EntityId));
        Assert.Equal(SerializeEntities(expected.Entities),
            SerializeEntities(received.SelectMany(packet => packet.Packet.Entities)));
    }

    private static byte[] Serialize(SnapshotChunkPacket packet) {
        var writer = new NetDataWriter();
        new PacketRegistry().Write(writer, packet);
        return writer.CopyData();
    }

    private static byte[] SerializeEntities(IEnumerable<EntitySnapshot> entities) {
        var writer = new NetDataWriter();
        foreach (var entity in entities) {
            entity.WriteTo(writer);
        }
        return writer.CopyData();
    }

    private static void AssertIoMetrics(NetServer server, IEnumerable<ExpectedSend> sends) {
        var io = server.CollectAndResetMetrics();
        Assert.Equal(sends.Sum(send => (long)send.Bytes.Length), io.SendCalls);
        Assert.Equal(sends.Sum(send => send.Bytes.Sum(bytes => (long)bytes.Length)), io.SendBytes);
    }

    private sealed record ExpectedSend(int Recipient, uint FrameId, DeliveryMethod Delivery,
        List<EntitySnapshot> Entities, int TargetBytes, int EntitiesPerChunk, byte[][] Bytes);

    private sealed record ReceivedChunk(byte[] Bytes, SnapshotChunkPacket Packet, DeliveryMethod Delivery);

    private sealed class LoopbackPeers : IDisposable {
        private readonly List<NetManager> _managers = [];
        public NetPeer[] Peers { get; } = new NetPeer[2];
        public List<ReceivedChunk>[] Received { get; } = [[], []];

        public LoopbackPeers() {
            try {
                var sender = Start(new EventBasedNetListener());
                for (var i = 0; i < Peers.Length; i++) {
                    var received = Received[i];
                    var listener = new EventBasedNetListener();
                    listener.ConnectionRequestEvent += request => request.AcceptIfKey("snapshot-regression");
                    listener.NetworkReceiveEvent += (_, reader, _, delivery) => {
                        var bytes = reader.GetRemainingBytes();
                        var decoder = new NetDataReader(bytes);
                        Assert.True(new PacketRegistry().TryRead(decoder, out var packet));
                        Assert.Equal(0, decoder.AvailableBytes);
                        received.Add(new ReceivedChunk(bytes, Assert.IsType<SnapshotChunkPacket>(packet), delivery));
                    };
                    var receiver = Start(listener);
                    Peers[i] = sender.Connect("127.0.0.1", receiver.LocalPort, "snapshot-regression");
                }
                WaitUntil(() => Peers.All(peer => peer.ConnectionState == ConnectionState.Connected)
                    && _managers.Skip(1).All(manager => manager.ConnectedPeersCount == 1));
            } catch {
                Dispose();
                throw;
            }
        }

        private NetManager Start(EventBasedNetListener listener) {
            var manager = new NetManager(listener) { AutoRecycle = true };
            _managers.Add(manager);
            Assert.True(manager.Start(0), "Failed to bind an ephemeral loopback-test socket.");
            return manager;
        }

        public void WaitUntil(Func<bool> condition) {
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(5)) {
                foreach (var manager in _managers) {
                    manager.PollEvents();
                }
                if (condition()) {
                    return;
                }
                Thread.Sleep(5);
            }
            Assert.True(condition(), "Timed out after five seconds waiting for loopback traffic.");
        }

        public void Dispose() {
            foreach (var manager in _managers) {
                manager.Stop();
            }
        }
    }
}
