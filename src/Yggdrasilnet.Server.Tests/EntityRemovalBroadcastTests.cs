using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using LiteNetLib;
using LiteNetLib.Utils;
using Yggdrasilnet.Server.Services;
using Yggdrasilnet.Server.Simulation;
using Yggdrasilnet.Server.Simulation.Managers;
using Yggdrasilnet.Server.Simulation.Session;
using Yggdrasilnet.Server.Simulation.Snapshot;
using Yggdrasilnet.Server.Simulation.World;
using Yggdrasilnet.Shared.Network.Packet;
using Yggdrasilnet.Shared.Network.Packet.Packets;
using ServerSimulation = Yggdrasilnet.Server.Simulation.Simulation;

namespace Yggdrasilnet.Server.Tests;

public sealed class EntityRemovalBroadcastTests {
    [Fact]
    public void DrainReturnsStableHistoryAndRecordsOnlySuccessfulDespawns() {
        var world = new World();
        var first = world.Spawn();
        var second = world.Spawn();
        var third = world.Spawn();
        var initiallyEmpty = world.DrainRemovedEntityIds();
        Assert.Empty(initiallyEmpty);

        Assert.True(world.Despawn(first.Id));
        Assert.False(world.Despawn(first.Id));
        Assert.False(world.Despawn(int.MaxValue));
        Assert.False(world.Despawn(-1));
        var firstDrain = world.DrainRemovedEntityIds();
        Assert.Equal(new[] { first.Id }, firstDrain);
        Assert.Empty(world.DrainRemovedEntityIds());
        Assert.False(world.TryGetEntity(first.Id, out _));

        Assert.True(world.Despawn(second.Id));
        Assert.Equal(new[] { first.Id }, firstDrain);
        Assert.Equal(new[] { second.Id }, world.DrainRemovedEntityIds());
        Assert.True(world.Despawn(third.Id));
        Assert.Equal(new[] { third.Id }, world.DrainRemovedEntityIds());
        Assert.Empty(world.DrainRemovedEntityIds());
        Assert.Equal(new[] { first.Id }, firstDrain);
        Assert.Empty(initiallyEmpty);
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void SpawnIdsRemainMonotonicAfterRemovingNewestAndEmptyingWorld() {
        var world = new World();
        var first = world.Spawn();
        var newest = world.Spawn();
        Assert.True(newest.Id > first.Id);
        Assert.True(world.Despawn(newest.Id));
        var replacement = world.Spawn();
        Assert.True(replacement.Id > newest.Id);

        Assert.True(world.Despawn(first.Id));
        Assert.True(world.Despawn(replacement.Id));
        Assert.Empty(world.Entities);
        var removed = world.DrainRemovedEntityIds();
        AssertIds([first.Id, newest.Id, replacement.Id], removed);
        var afterEmpty = world.Spawn();
        Assert.True(afterEmpty.Id > replacement.Id);
        Assert.DoesNotContain(afterEmpty.Id, removed);
        Assert.Same(afterEmpty, Assert.Single(world.Entities));
        Assert.Empty(world.DrainRemovedEntityIds());
    }

    [Fact]
    public void BatchDrainsOnceAndPrunesEverySessionBeforeResolvingItsOwner() {
        var world = new World();
        var first = world.Spawn();
        var next = world.Spawn();
        var live = world.Spawn();
        var builder = new SnapshotBuilder(world);
        var sessions = new[] { NewSession(-1), NewSession(int.MaxValue) };
        foreach (var session in sessions) {
            SeedStates(session, [first.Id, next.Id, live.Id]);
        }
        var liveStates = sessions.Select(session => session.LastSentEntities[live.Id]).ToArray();
        Assert.True(world.Despawn(first.Id));
        IReadOnlyList<int> retained;

        using (var batch = BeginBatch(builder, 61)) {
            retained = batch.RemovedEntityIds;
            Assert.Equal(new[] { first.Id }, retained);
            Assert.Empty(world.DrainRemovedEntityIds());
            Assert.True(world.Despawn(next.Id));
            foreach (var session in sessions) {
                Assert.Empty(batch.Build(session).Entities);
                Assert.False(session.LastSentEntities.ContainsKey(first.Id));
                Assert.True(session.LastSentEntities.ContainsKey(next.Id));
            }
            Assert.Equal(new[] { first.Id }, retained);
        }

        using (var batch = BeginBatch(builder, 61)) {
            Assert.Equal(new[] { next.Id }, batch.RemovedEntityIds);
            for (var i = 0; i < sessions.Length; i++) {
                Assert.Empty(batch.Build(sessions[i]).Entities);
                Assert.Equal(live.Id, Assert.Single(sessions[i].LastSentEntities).Key);
                Assert.Same(liveStates[i], sessions[i].LastSentEntities[live.Id]);
            }
        }
        using (var batch = BeginBatch(builder, 61)) {
            Assert.Empty(batch.RemovedEntityIds);
        }
        Assert.Equal(new[] { first.Id }, retained);
    }

    [Fact]
    public void LargeRemovalBatchesAreBoundedReliableAndCopiedAcrossPeersAndBroadcasts() {
        using var fixture = new BroadcastFixture();
        var sessions = new[] { fixture.Join(0, -1), fixture.Join(1, -1) };
        var capacities = fixture.Network.Peers.Select(MaxIdsPerPacket).ToArray();
        var firstIds = SpawnAndRemove(fixture.World, capacities.Max() * 2 + 17);
        foreach (var session in sessions) {
            SeedStates(session, firstIds);
        }
        fixture.SetTick(61);
        var first = fixture.Service.Broadcast();
        var firstPacketCounts = capacities.Select(capacity => PacketCount(firstIds.Length, capacity)).ToArray();
        AssertRemovalOnlyMetrics(first, 2, firstPacketCounts.Sum(), firstIds.Length * 2);
        Assert.All(sessions, session => Assert.Empty(session.LastSentEntities));

        var secondIds = SpawnAndRemove(fixture.World, 7);
        foreach (var session in sessions) {
            SeedStates(session, secondIds);
        }
        var second = fixture.Service.Broadcast();
        AssertRemovalOnlyMetrics(second, 2, 2, secondIds.Length * 2);
        Assert.All(sessions, session => Assert.Empty(session.LastSentEntities));
        for (var i = 0; i < 3; i++) {
            AssertRemovalOnlyMetrics(fixture.Service.Broadcast(), 2, 0, 0);
        }

        // Queue both waves for both peers before polling: the packet and writer have already been reused.
        fixture.Network.WaitForTraffic(first.DespawnPacketsSent + second.DespawnPacketsSent);
        for (var i = 0; i < sessions.Length; i++) {
            var received = fixture.Network.Received[i];
            Assert.Equal(firstPacketCounts[i] + 1, received.Count);
            Assert.True(firstPacketCounts[i] >= 3);
            AssertRemovalPackets(fixture.Network.Peers[i], received.Take(firstPacketCounts[i]), firstIds);
            AssertRemovalPackets(fixture.Network.Peers[i], received.Skip(firstPacketCounts[i]), secondIds);
            Assert.All(received.Take(firstPacketCounts[i] - 1), packet =>
                Assert.Equal(capacities[i], Assert.IsType<DespawnEntitiesPacket>(packet.Packet).EntityIds.Count));
        }
        Assert.Empty(fixture.World.DrainRemovedEntityIds());
        var io = fixture.Server.CollectAndResetMetrics();
        Assert.Equal((long)(first.DespawnPacketsSent + second.DespawnPacketsSent), io.SendCalls);
        Assert.Equal(fixture.Network.Received.SelectMany(packets => packets).Sum(packet => (long)packet.Bytes.Length),
            io.SendBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyWorldStillBroadcastsRemovalsAndClearsCachesForMissingOwners(bool unassignedOwner) {
        using var fixture = new BroadcastFixture();
        var owners = new[] { fixture.World.Spawn(), fixture.World.Spawn(Vector3.UnitX) };
        var target = fixture.World.Spawn(new Vector3(2, 0, 0));
        var sessions = new[] { fixture.Join(0, owners[0].Id), fixture.Join(1, owners[1].Id) };
        var ids = owners.Select(entity => entity.Id).Append(target.Id).ToArray();
        foreach (var session in sessions) {
            AssertIds(ids, SnapshotIds(fixture.Builder.BuildSnapshotForSession(session, 60, 60, true)));
        }
        Assert.NotEmpty(SpatialCache(fixture.Builder, "_interestGrid"));
        Assert.NotEmpty(SpatialCache(fixture.Builder, "_nearbyEntitiesByCell"));
        foreach (var id in ids) {
            Assert.True(fixture.World.Despawn(id));
        }
        if (unassignedOwner) {
            foreach (var session in sessions) {
                session.EntityId = -1;
            }
        }

        fixture.SetTick(60);
        var metrics = fixture.Service.Broadcast();

        AssertRemovalOnlyMetrics(metrics, 2, 2, ids.Length * 2);
        Assert.True(metrics.Keyframe);
        Assert.All(sessions, session => Assert.Empty(session.LastSentEntities));
        Assert.Empty(SpatialCache(fixture.Builder, "_interestGrid"));
        Assert.Empty(SpatialCache(fixture.Builder, "_nearbyEntitiesByCell"));
        Assert.Empty(fixture.World.Entities);
        fixture.Network.WaitForTraffic(2);
        for (var i = 0; i < sessions.Length; i++) {
            AssertRemovalPackets(fixture.Network.Peers[i], fixture.Network.Received[i], ids);
        }
        AssertRemovalOnlyMetrics(fixture.Service.Broadcast(), 2, 0, 0);
        fixture.Network.WaitForTraffic(2);
    }

    [Fact]
    public void BroadcastWithoutSessionsDrainsHistorySoLaterJoinsReceiveOnlyNewRemovals() {
        using var fixture = new BroadcastFixture();
        var oldIds = SpawnAndRemove(fixture.World, 3);

        AssertRemovalOnlyMetrics(fixture.Service.Broadcast(), 0, 0, 0);
        Assert.Empty(fixture.World.DrainRemovedEntityIds());
        AssertRemovalOnlyMetrics(fixture.Service.Broadcast(), 0, 0, 0);
        Assert.Equal(0L, fixture.Server.CollectAndResetMetrics().SendCalls);

        var owner = fixture.World.Spawn();
        var session = fixture.Join(0, owner.Id);
        fixture.SetTick(60);
        var joined = fixture.Service.Broadcast();
        Assert.Equal(1, joined.Sessions);
        Assert.Equal(1, joined.SnapshotEntities);
        Assert.Equal(1, joined.ChunksSent);
        Assert.Equal(0, joined.DespawnPacketsSent);
        Assert.Equal(0, joined.DespawnEntities);
        Assert.True(fixture.World.Despawn(owner.Id));
        var removed = fixture.Service.Broadcast();
        AssertRemovalOnlyMetrics(removed, 1, 1, 1);
        Assert.Empty(session.LastSentEntities);

        fixture.Network.WaitForTraffic(2);
        Assert.Empty(fixture.Network.Received[1]);
        var received = fixture.Network.Received[0];
        Assert.Equal(owner.Id, Assert.Single(Assert.IsType<SnapshotChunkPacket>(received[0].Packet).Entities).EntityId);
        AssertRemovalPackets(fixture.Network.Peers[0], received.Skip(1), [owner.Id]);
        Assert.All(received.Where(packet => packet.Packet is DespawnEntitiesPacket), packet =>
            Assert.All(Assert.IsType<DespawnEntitiesPacket>(packet.Packet).EntityIds,
                id => Assert.DoesNotContain(id, oldIds)));
    }

    [Theory]
    [InlineData(60L, true)]
    [InlineData(61L, false)]
    public void SameTickRemovalInvalidatesBothSpatialCachesWithoutLosingLiveBaselines(long tick, bool keyframe) {
        using var fixture = new BroadcastFixture();
        var owners = new[] { fixture.World.Spawn(Vector3.UnitX), fixture.World.Spawn(new Vector3(2, 0, 0)) };
        var live = fixture.World.Spawn(new Vector3(3, 0, 0));
        var removed = fixture.World.Spawn(new Vector3(4, 0, 0));
        var entering = fixture.World.Spawn(new Vector3(1000, 0, 0));
        var sessions = new[] { fixture.Join(0, owners[0].Id), fixture.Join(1, owners[1].Id) };
        var oldIds = owners.Select(entity => entity.Id).Append(live.Id).Append(removed.Id).ToArray();
        var retained = sessions.Select(session =>
            fixture.Builder.BuildSnapshotForSession(session, tick, 60, true)).ToArray();
        Assert.All(retained, packet => AssertIds(oldIds, SnapshotIds(packet)));
        var baselines = sessions.Select(session => session.LastSentEntities
            .Where(entry => entry.Key != removed.Id).ToDictionary(entry => entry.Key, entry => entry.Value)).ToArray();
        Assert.Contains(removed.Id, SpatialIds(fixture.Builder, "_interestGrid"));
        Assert.Contains(removed.Id, SpatialIds(fixture.Builder, "_nearbyEntitiesByCell"));
        Assert.DoesNotContain(entering.Id, SpatialIds(fixture.Builder, "_nearbyEntitiesByCell"));

        Assert.True(fixture.World.Despawn(removed.Id));
        // A newly nearby entity also proves both cached grid membership and candidate lists were rebuilt.
        entering.Position = new Vector3(5, 0, 0);
        fixture.SetTick(tick);
        var metrics = fixture.Service.Broadcast();

        Assert.Equal(keyframe, metrics.Keyframe);
        Assert.Equal(2, metrics.Sessions);
        Assert.Equal(2, metrics.DespawnPacketsSent);
        Assert.Equal(2, metrics.DespawnEntities);
        Assert.Equal(2, metrics.ChunksSent);
        Assert.Equal(keyframe ? 8 : 4, metrics.SnapshotEntities);
        Assert.DoesNotContain(removed.Id, SpatialIds(fixture.Builder, "_interestGrid"));
        Assert.DoesNotContain(removed.Id, SpatialIds(fixture.Builder, "_nearbyEntitiesByCell"));
        Assert.Contains(entering.Id, SpatialIds(fixture.Builder, "_nearbyEntitiesByCell"));
        var liveIds = owners.Select(entity => entity.Id).Append(live.Id).Append(entering.Id).ToArray();
        for (var i = 0; i < sessions.Length; i++) {
            AssertIds(liveIds, sessions[i].LastSentEntities.Keys);
            foreach (var (id, state) in baselines[i]) {
                Assert.Same(state, sessions[i].LastSentEntities[id]);
                Assert.Equal(tick, state.LastSentTick);
                Assert.Equal(tick, state.LastFullSentTick);
            }
        }

        fixture.Network.WaitForTraffic(4);
        for (var i = 0; i < sessions.Length; i++) {
            var received = fixture.Network.Received[i];
            AssertRemovalPackets(fixture.Network.Peers[i],
                received.Where(packet => packet.Packet is DespawnEntitiesPacket), [removed.Id]);
            var snapshot = Assert.Single(received.Where(packet => packet.Packet is SnapshotChunkPacket));
            Assert.Equal(keyframe ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable, snapshot.Delivery);
            var packet = Assert.IsType<SnapshotChunkPacket>(snapshot.Packet);
            Assert.DoesNotContain(removed.Id, packet.Entities.Select(entity => entity.EntityId));
            AssertIds(keyframe ? liveIds : new[] { owners[i].Id, entering.Id },
                packet.Entities.Select(entity => entity.EntityId));
        }
        Assert.All(retained, packet => AssertIds(oldIds, SnapshotIds(packet)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandaloneSameTickSnapshotSkipsDespawnedCachedCandidates(bool forceFullSnapshot) {
        var world = new World();
        var owner = world.Spawn();
        var removed = world.Spawn(Vector3.UnitX);
        var live = world.Spawn(new Vector3(2, 0, 0));
        var builder = new SnapshotBuilder(world);
        var retained = builder.BuildSnapshotForSession(NewSession(owner.Id), 60, 60, true);
        AssertIds([owner.Id, removed.Id, live.Id], SnapshotIds(retained));
        Assert.True(world.Despawn(removed.Id));
        Assert.Contains(removed.Id, SpatialIds(builder, "_nearbyEntitiesByCell"));

        // A fresh baseline makes stale candidates eligible even for a non-keyframe at the same tick.
        var session = NewSession(owner.Id);
        var snapshot = builder.BuildSnapshotForSession(session, 60, 60, forceFullSnapshot);

        AssertIds([owner.Id, live.Id], SnapshotIds(snapshot));
        AssertIds([owner.Id, live.Id], session.LastSentEntities.Keys);
        AssertIds([owner.Id, removed.Id, live.Id], SnapshotIds(retained));
        Assert.Equal(new[] { removed.Id }, world.DrainRemovedEntityIds());
    }

    [Fact]
    public void ReliableKeyframeRemovalAndLaterKeyframesArriveInOrderWithoutResurrection() {
        using var fixture = new BroadcastFixture();
        var owners = new[] { fixture.World.Spawn(), fixture.World.Spawn(Vector3.UnitX) };
        var removed = fixture.World.Spawn(new Vector3(2, 0, 0));
        fixture.Join(0, owners[0].Id);
        fixture.Join(1, owners[1].Id);
        fixture.SetTick(60);
        var oldFrame = fixture.Service.Broadcast();
        Assert.True(fixture.World.Despawn(removed.Id));
        fixture.SetTick(120);
        var removalFrame = fixture.Service.Broadcast();
        fixture.SetTick(180);
        var laterFrame = fixture.Service.Broadcast();

        Assert.All(new[] { oldFrame, removalFrame, laterFrame }, metrics => {
            Assert.True(metrics.Keyframe);
            Assert.Equal(2, metrics.ChunksSent);
        });
        Assert.Equal(0, oldFrame.DespawnPacketsSent);
        Assert.Equal(2, removalFrame.DespawnPacketsSent);
        Assert.Equal(2, removalFrame.DespawnEntities);
        Assert.Equal(0, laterFrame.DespawnPacketsSent);
        Assert.Equal(0, laterFrame.DespawnEntities);
        fixture.Network.WaitForTraffic(8);
        for (var i = 0; i < owners.Length; i++) {
            var received = fixture.Network.Received[i];
            Assert.Equal(4, received.Count);
            Assert.All(received, packet => Assert.Equal(DeliveryMethod.ReliableOrdered, packet.Delivery));
            var before = Assert.IsType<SnapshotChunkPacket>(received[0].Packet);
            Assert.Equal(1u, before.FrameId);
            AssertIds(owners.Select(entity => entity.Id).Append(removed.Id),
                before.Entities.Select(entity => entity.EntityId));
            AssertRemovalPackets(fixture.Network.Peers[i], received.Skip(1).Take(1), [removed.Id]);
            for (var index = 2; index < received.Count; index++) {
                var after = Assert.IsType<SnapshotChunkPacket>(received[index].Packet);
                Assert.Equal((uint)index, after.FrameId);
                Assert.Equal((ushort)0, after.ChunkIndex);
                Assert.True(after.IsLastChunk);
                AssertIds(owners.Select(entity => entity.Id), after.Entities.Select(entity => entity.EntityId));
            }
        }
    }

    [Fact]
    public void RemovePlayerDespawnsClearsDetachedCacheAndBroadcastsOnlyToRemainingPeer() {
        using var fixture = new BroadcastFixture();
        var owners = new[] { fixture.World.Spawn(), fixture.World.Spawn(Vector3.UnitX) };
        var live = fixture.World.Spawn(new Vector3(2, 0, 0));
        var detached = fixture.Join(0, owners[0].Id);
        var remaining = fixture.Join(1, owners[1].Id);
        fixture.SetTick(60);
        var warmup = fixture.Service.Broadcast();
        Assert.Equal(2, warmup.ChunksSent);
        fixture.Network.WaitForTraffic(2);
        fixture.Network.ClearReceived();
        fixture.Server.CollectAndResetMetrics();
        AssertIds([owners[0].Id, owners[1].Id, live.Id], detached.LastSentEntities.Keys);
        var liveBaseline = remaining.LastSentEntities[live.Id];
        var manager = new PlayerSimulationManager(fixture.Sessions, null!, null!, fixture.World);

        // Keep the transport connected so an accidental send to the detached session is observable.
        manager.RemovePlayer(fixture.Network.Peers[0], default);
        Assert.Empty(detached.LastSentEntities);
        Assert.False(fixture.Sessions.TryGet(fixture.Network.Peers[0], out _));
        Assert.Same(remaining, Assert.Single(fixture.Sessions.All));
        Assert.False(fixture.World.TryGetEntity(owners[0].Id, out _));
        Assert.True(fixture.World.TryGetEntity(owners[1].Id, out _));
        Assert.True(remaining.LastSentEntities.ContainsKey(owners[0].Id));
        manager.RemovePlayer(fixture.Network.Peers[0], default);

        var metrics = fixture.Service.Broadcast();

        Assert.Equal(1, metrics.Sessions);
        Assert.Equal(1, metrics.DespawnPacketsSent);
        Assert.Equal(1, metrics.DespawnEntities);
        Assert.Equal(1, metrics.ChunksSent);
        Assert.Equal(2, metrics.SnapshotEntities);
        AssertIds([owners[1].Id, live.Id], remaining.LastSentEntities.Keys);
        Assert.Same(liveBaseline, remaining.LastSentEntities[live.Id]);
        Assert.Empty(detached.LastSentEntities);
        Assert.Empty(fixture.World.DrainRemovedEntityIds());
        fixture.Network.WaitForTraffic(2);
        Assert.Empty(fixture.Network.Received[0]);
        var received = fixture.Network.Received[1];
        AssertRemovalPackets(fixture.Network.Peers[1], received.Take(1), [owners[0].Id]);
        var snapshot = Assert.IsType<SnapshotChunkPacket>(received[1].Packet);
        AssertIds([owners[1].Id, live.Id], snapshot.Entities.Select(entity => entity.EntityId));
        Assert.Equal(2L, fixture.Server.CollectAndResetMetrics().SendCalls);
        var repeated = fixture.Service.Broadcast();
        Assert.Equal(0, repeated.DespawnPacketsSent);
        Assert.Equal(0, repeated.DespawnEntities);
        fixture.Network.WaitForTraffic(3);
        Assert.Empty(fixture.Network.Received[0]);
    }

    private static int[] SpawnAndRemove(World world, int count) {
        var ids = Enumerable.Range(0, count).Select(_ => world.Spawn().Id).ToArray();
        foreach (var id in ids) {
            Assert.True(world.Despawn(id));
            Assert.False(world.Despawn(id));
        }
        return ids;
    }

    private static PlayerSession NewSession(int entityId) =>
        new((NetPeer)RuntimeHelpers.GetUninitializedObject(typeof(NetPeer)), 0) { EntityId = entityId };

    private static void SeedStates(PlayerSession session, IEnumerable<int> ids) {
        foreach (var id in ids) {
            session.LastSentEntities[id] = new SentEntityState { LastSentTick = 60, LastObservedTick = 60 };
        }
    }

    private static int[] SnapshotIds(SnapshotPacket packet) =>
        packet.Entities.Select(entity => entity.EntityId).ToArray();

    private static void AssertIds(IEnumerable<int> expected, IEnumerable<int> actual) =>
        Assert.Equal(expected.OrderBy(id => id).ToArray(), actual.OrderBy(id => id).ToArray());

    private static int TargetBytes(NetPeer peer) =>
        Math.Min(1000, peer.GetMaxSinglePacketSize(DeliveryMethod.ReliableOrdered) - 96);

    private static int MaxIdsPerPacket(NetPeer peer) =>
        Math.Max(1, (TargetBytes(peer) - 3) / sizeof(int));

    private static int PacketCount(int ids, int capacity) => (ids + capacity - 1) / capacity;

    private static void AssertRemovalPackets(NetPeer peer, IEnumerable<ReceivedPacket> packets,
        IEnumerable<int> expectedIds) {
        var received = packets.ToArray();
        var expected = expectedIds.ToArray();
        Assert.Equal(PacketCount(expected.Length, MaxIdsPerPacket(peer)), received.Length);
        var actual = new List<int>();
        foreach (var wire in received) {
            Assert.Equal(DeliveryMethod.ReliableOrdered, wire.Delivery);
            var packet = Assert.IsType<DespawnEntitiesPacket>(wire.Packet);
            Assert.Equal((byte)8, wire.Bytes[0]);
            Assert.InRange(packet.EntityIds.Count, 1, MaxIdsPerPacket(peer));
            Assert.InRange(wire.Bytes.Length, 1, TargetBytes(peer));
            Assert.Equal(3 + packet.EntityIds.Count * sizeof(int), wire.Bytes.Length);
            var writer = new NetDataWriter();
            new PacketRegistry().Write(writer, new DespawnEntitiesPacket { EntityIds = packet.EntityIds.ToList() });
            Assert.Equal(writer.CopyData(), wire.Bytes);
            actual.AddRange(packet.EntityIds);
        }
        AssertIds(expected, actual);
        Assert.Equal(actual.Count, actual.Distinct().Count());
    }

    private static void AssertRemovalOnlyMetrics(SnapshotBroadcastMetrics metrics, int sessions,
        int packets, int entities) {
        Assert.Equal(sessions, metrics.Sessions);
        Assert.Equal(packets, metrics.DespawnPacketsSent);
        Assert.Equal(entities, metrics.DespawnEntities);
        Assert.Equal(0, metrics.SnapshotEntities);
        Assert.Equal(0, metrics.ChunksSent);
        Assert.Equal(0, metrics.DroppedOversizedEntities);
    }

    private static IDictionary SpatialCache(SnapshotBuilder builder, string name) {
        var field = typeof(SnapshotBuilder).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<IDictionary>(field.GetValue(builder));
    }

    private static int[] SpatialIds(SnapshotBuilder builder, string name) =>
        SpatialCache(builder, name).Values.Cast<List<Entity>>().SelectMany(entities => entities)
            .Select(entity => entity.Id).ToArray();

    private static ReflectedBatch BeginBatch(SnapshotBuilder builder, long tick) {
        var method = typeof(SnapshotBuilder).GetMethod("BeginBroadcastBatch",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var scope = method.Invoke(builder, new object[] { tick });
        Assert.NotNull(scope);
        return new ReflectedBatch(scope);
    }

    private sealed class ReflectedBatch(object scope) : IDisposable {
        public IReadOnlyList<int> RemovedEntityIds {
            get {
                var property = scope.GetType().GetProperty("RemovedEntityIds");
                Assert.NotNull(property);
                return Assert.IsAssignableFrom<IReadOnlyList<int>>(property.GetValue(scope));
            }
        }

        public SnapshotPacket Build(PlayerSession session) {
            var method = scope.GetType().GetMethod("Build");
            Assert.NotNull(method);
            return Assert.IsType<SnapshotPacket>(method.Invoke(scope, new object[] { session, 60, false }));
        }

        public void Dispose() => Assert.IsAssignableFrom<IDisposable>(scope).Dispose();
    }

    private sealed class BroadcastFixture : IDisposable {
        public World World { get; } = new();
        public PlayerSessionRegistry Sessions { get; } = new();
        public SnapshotBuilder Builder { get; }
        public ServerSimulation Simulation { get; }
        public NetServer Server { get; }
        public SnapshotBroadcastService Service { get; }
        public LoopbackPeers Network { get; }

        public BroadcastFixture() {
            Builder = new SnapshotBuilder(World);
            // Do not load content or initialize spell systems just to exercise snapshot broadcasting.
            Simulation = (ServerSimulation)RuntimeHelpers.GetUninitializedObject(typeof(ServerSimulation));
            SetSimulationField("_world", World);
            SetSimulationField("_sessions", Sessions);
            SetSimulationField("_snapshotBuilder", Builder);
            SetTick(60);
            Server = new NetServer(new ConcurrentQueue<ISimulationEvent>());
            try {
                Network = new LoopbackPeers();
                Service = new SnapshotBroadcastService(Server, Simulation, 60);
            } catch {
                Server.Stop();
                throw;
            }
        }

        public PlayerSession Join(int peer, int entityId) {
            var session = Sessions.Create(Network.Peers[peer], Simulation.Tick);
            session.EntityId = entityId;
            return session;
        }

        public void SetTick(long tick) => SetSimulationField("<Tick>k__BackingField", tick);

        private void SetSimulationField(string name, object value) {
            var field = typeof(ServerSimulation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(Simulation, value);
        }

        public void Dispose() {
            try {
                Server.Stop();
            } finally {
                Network.Dispose();
            }
        }
    }

    private sealed record ReceivedPacket(byte[] Bytes, IPacket Packet, DeliveryMethod Delivery);

    private sealed class LoopbackPeers : IDisposable {
        private readonly List<NetManager> _managers = [];
        public NetPeer[] Peers { get; } = new NetPeer[2];
        public List<ReceivedPacket>[] Received { get; } = [[], []];

        public LoopbackPeers() {
            try {
                var sender = Start(new EventBasedNetListener());
                for (var i = 0; i < Peers.Length; i++) {
                    var received = Received[i];
                    var listener = new EventBasedNetListener();
                    listener.ConnectionRequestEvent += request => request.AcceptIfKey("entity-removal-regression");
                    listener.NetworkReceiveEvent += (_, reader, _, delivery) => {
                        var bytes = reader.GetRemainingBytes();
                        var decoder = new NetDataReader(bytes);
                        Assert.True(new PacketRegistry().TryRead(decoder, out var packet));
                        Assert.Equal(0, decoder.AvailableBytes);
                        Assert.True(packet is DespawnEntitiesPacket or SnapshotChunkPacket);
                        received.Add(new ReceivedPacket(bytes, packet, delivery));
                    };
                    var receiver = Start(listener);
                    Peers[i] = sender.Connect("127.0.0.1", receiver.LocalPort, "entity-removal-regression");
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

        public void WaitForTraffic(int expectedPackets) {
            WaitUntil(() => Received.Sum(packets => packets.Count) >= expectedPackets);
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromMilliseconds(150)) {
                Poll();
                Thread.Sleep(5);
            }
            Assert.Equal(expectedPackets, Received.Sum(packets => packets.Count));
        }

        private void WaitUntil(Func<bool> condition) {
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(5)) {
                Poll();
                if (condition()) {
                    return;
                }
                Thread.Sleep(5);
            }
            Assert.True(condition(), "Timed out after five seconds waiting for loopback traffic.");
        }

        private void Poll() {
            foreach (var manager in _managers) {
                manager.PollEvents();
            }
        }

        public void ClearReceived() {
            foreach (var received in Received) {
                received.Clear();
            }
        }

        public void Dispose() {
            foreach (var manager in _managers) {
                manager.Stop();
            }
        }
    }
}
