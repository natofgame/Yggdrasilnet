using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using LiteNetLib;
using LiteNetLib.Utils;
using Yggdrasilnet.Server.Simulation.Session;
using Yggdrasilnet.Server.Simulation.Snapshot;
using Yggdrasilnet.Server.Simulation.World;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Network.Packet.Packets;
using Yggdrasilnet.Shared.Network.Packet.Snapshot;

namespace Yggdrasilnet.Server.Tests;

public sealed class SnapshotBuilderTests {
    [Theory]
    [InlineData(-1)]
    [InlineData(12345)]
    public void MissingOwnerReturnsIndependentEmptyPacketsWithoutChangingSession(int entityId) {
        var builder = new SnapshotBuilder(new World());
        var session = NewSession(entityId);
        session.SnapshotRoundRobinOffset = -7;
        session.LastSentEntities[42] = new SentEntityState { LastObservedTick = 1 };

        var first = builder.BuildSnapshotForSession(session, 1000, 60, true);
        var second = builder.BuildSnapshotForSession(session, 1000, 60, true);

        Assert.Empty(first.Entities);
        Assert.Empty(second.Entities);
        Assert.NotSame(first, second);
        Assert.NotSame(first.Entities, second.Entities);
        Assert.Equal(-7, session.SnapshotRoundRobinOffset);
        Assert.Equal(1L, session.LastSentEntities[42].LastObservedTick);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void CapIsExactly220WithOwnerFirstAndNormalizedRoundRobinOrder(int initialOffset) {
        var world = new World();
        var candidates = Enumerable.Range(0, 300).Select(_ => world.Spawn()).ToList();
        var owner = world.Spawn();
        candidates.Add(owner);
        var builder = new SnapshotBuilder(world);
        var session = NewSession(owner.Id);
        session.SnapshotRoundRobinOffset = initialOffset;

        for (var pass = 0; pass < 2; pass++) {
            var start = ((session.SnapshotRoundRobinOffset % candidates.Count) + candidates.Count) % candidates.Count;
            var expected = new[] { owner.Id }.Concat(
                Enumerable.Range(0, candidates.Count)
                    .Select(i => candidates[(start + i) % candidates.Count].Id)
                    .Where(id => id != owner.Id)
                    .Take(219)).ToArray();

            var packet = builder.BuildSnapshotForSession(session, 10, 60, true);

            Assert.Equal(220, packet.Entities.Count);
            Assert.Equal(expected, Ids(packet));
            Assert.Equal(220, packet.Entities.Select(entity => entity.EntityId).Distinct().Count());
            Assert.Equal((start + 220) % candidates.Count, session.SnapshotRoundRobinOffset);
        }
    }

    [Fact]
    public void CandidateOrderIsGridRowThenColumnThenBucketInsertionOrder() {
        var world = new World();
        var owner = world.Spawn();
        var upperRight = world.Spawn(new Vector3(21, 0, 21));
        var lowerRight = world.Spawn(new Vector3(1, 0, -1));
        var upperLeft = world.Spawn(new Vector3(-1, 0, 1));
        var lowerLeftFirst = world.Spawn(new Vector3(-2, 0, -2));
        var lowerLeftSecond = world.Spawn(new Vector3(-1, 0, -1));
        var builder = new SnapshotBuilder(world);
        var expected = new[] {
            owner.Id, lowerLeftFirst.Id, lowerLeftSecond.Id, lowerRight.Id, upperLeft.Id, upperRight.Id
        };

        var first = builder.BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true);
        var cached = builder.BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true);

        Assert.Equal(expected, Ids(first));
        Assert.Equal(expected, Ids(cached));
    }

    [Fact]
    public void FarthestTierIsCappedAt32WithoutConsumingNearTierBudget() {
        var world = new World();
        var owner = world.Spawn();
        var far = Enumerable.Range(0, 40)
            .Select(_ => world.Spawn(new Vector3(-30, 0, 0))).ToArray();
        var near = Enumerable.Range(0, 50)
            .Select(_ => world.Spawn(new Vector3(1, 0, 0))).ToArray();
        var builder = new SnapshotBuilder(world);
        var session = NewSession(owner.Id);

        var packet = builder.BuildSnapshotForSession(session, 1, 60, true);

        Assert.Equal(new[] { owner.Id }.Concat(far.Take(32).Select(e => e.Id))
            .Concat(near.Select(e => e.Id)).ToArray(), Ids(packet));
        Assert.Equal(83, packet.Entities.Count);
        Assert.All(far.Skip(32), entity => Assert.False(session.LastSentEntities.ContainsKey(entity.Id)));
    }

    [Theory]
    [InlineData(12f, 0f, true)]
    [InlineData(28f, 0f, true)]
    [InlineData(45f, 0f, true)]
    [InlineData(-45f, 0f, true)]
    [InlineData(0f, -45f, true)]
    [InlineData(27f, 36f, true)]
    [InlineData(45.001f, 0f, false)]
    [InlineData(32f, 32f, false)]
    public void InterestUsesInclusiveCircularXZDistanceIgnoringHeight(float x, float z, bool included) {
        var world = new World();
        var owner = world.Spawn();
        var target = world.Spawn(new Vector3(x, 10000, z));

        var packet = new SnapshotBuilder(world).BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true);

        Assert.Equal(included, packet.Entities.Any(entity => entity.EntityId == target.Id));
        Assert.Equal(owner.Id, packet.Entities[0].EntityId);
    }

    [Theory]
    [InlineData(12f, 3)]
    [InlineData(12.001f, 10)]
    [InlineData(28f, 10)]
    [InlineData(28.001f, 60)]
    [InlineData(45f, 60)]
    public void DistanceTiersSendAtTheirExactCadence(float distance, int interval) {
        var world = new World();
        var owner = world.Spawn();
        var target = world.Spawn(new Vector3(distance, 0, 0));
        target.AddComponent(new VelocityComponent { X = 1 });
        var builder = new SnapshotBuilder(world);
        var session = NewSession(owner.Id);
        Assert.Contains(target.Id, Ids(builder.BuildSnapshotForSession(session, 0, 60, false)));
        target.Position += new Vector3(0, 1, 0);

        Assert.DoesNotContain(target.Id, Ids(builder.BuildSnapshotForSession(session, interval - 1, 60, false)));
        Assert.Equal((long)interval - 1, session.LastSentEntities[target.Id].LastObservedTick);
        Assert.Equal(0L, session.LastSentEntities[target.Id].LastSentTick);
        Assert.Contains(target.Id, Ids(builder.BuildSnapshotForSession(session, interval, 60, false)));
        Assert.Equal((long)interval, session.LastSentEntities[target.Id].LastSentTick);
    }

    [Theory]
    [InlineData(false, 0f, 3)]
    [InlineData(true, 0f, 30)]
    [InlineData(true, 0.01f, 30)]
    [InlineData(true, 0.0101f, 3)]
    public void StationaryThrottleRequiresVelocityAndUsesInclusiveSpeedThreshold(
        bool hasVelocity, float speed, int interval) {
        var world = new World();
        var owner = world.Spawn();
        var target = world.Spawn();
        if (hasVelocity) {
            target.AddComponent(new VelocityComponent { Y = speed });
        }
        var builder = new SnapshotBuilder(world);
        var session = NewSession(owner.Id);
        builder.BuildSnapshotForSession(session, 0, 60, true);
        target.Position = new Vector3(0, 1, 0);

        Assert.DoesNotContain(target.Id, Ids(builder.BuildSnapshotForSession(session, interval - 1, 60, false)));
        Assert.Contains(target.Id, Ids(builder.BuildSnapshotForSession(session, interval, 60, false)));
    }

    [Theory]
    [InlineData(0f, 0f, false)]
    [InlineData(0.099f, 0.049f, false)]
    [InlineData(0.1f, 0f, true)]
    [InlineData(0f, 0.05f, true)]
    public void DueEntityStillRequiresInclusivePositionOrVelocitySignificance(
        float positionDelta, float velocityDelta, bool included) {
        var world = new World();
        var owner = world.Spawn();
        var target = world.Spawn();
        var velocity = new VelocityComponent();
        target.AddComponent(velocity);
        var builder = new SnapshotBuilder(world);
        var session = NewSession(owner.Id);
        builder.BuildSnapshotForSession(session, 0, 60, true);
        target.Position = new Vector3(0, positionDelta, 0);
        velocity.Z = velocityDelta;

        var packet = builder.BuildSnapshotForSession(session, 30, 60, false);

        Assert.Equal(included, packet.Entities.Any(entity => entity.EntityId == target.Id));
        Assert.Equal(30L, session.LastSentEntities[target.Id].LastObservedTick);
        Assert.Equal(included ? 30L : 0L, session.LastSentEntities[target.Id].LastSentTick);
        Assert.Equal(0L, session.LastSentEntities[target.Id].LastFullSentTick);
    }

    [Fact]
    public void KeyframeBypassesCadenceAndSignificanceAndOwnerIsAlwaysIncluded() {
        var world = new World();
        var owner = world.Spawn();
        var target = world.Spawn(new Vector3(40, 0, 0));
        target.AddComponent(new VelocityComponent());
        var session = NewSession(owner.Id);
        var builder = new SnapshotBuilder(world);
        builder.BuildSnapshotForSession(session, 10, 60, true);

        Assert.Equal(new[] { owner.Id }, Ids(builder.BuildSnapshotForSession(session, 11, 60, false)));
        Assert.Equal(11L, session.LastSentEntities[owner.Id].LastSentTick);
        Assert.Equal(10L, session.LastSentEntities[owner.Id].LastFullSentTick);
        Assert.Equal(new[] { owner.Id, target.Id }, Ids(builder.BuildSnapshotForSession(session, 11, 60, true)));
        Assert.All(session.LastSentEntities.Values, state => {
            Assert.Equal(11L, state.LastSentTick);
            Assert.Equal(11L, state.LastObservedTick);
            Assert.Equal(11L, state.LastFullSentTick);
        });
    }

    [Fact]
    public void SentStatesPruneOnlyOnKeyframesAndStrictlyAfterThreeSecondTtl() {
        var world = new World();
        var owner = world.Spawn();
        var target = world.Spawn();
        var session = NewSession(owner.Id);
        var builder = new SnapshotBuilder(world);
        builder.BuildSnapshotForSession(session, 0, 60, true);
        Assert.True(world.Despawn(target.Id));

        builder.BuildSnapshotForSession(session, 180, 60, true);
        Assert.True(session.LastSentEntities.ContainsKey(target.Id));
        builder.BuildSnapshotForSession(session, 181, 60, false);
        Assert.True(session.LastSentEntities.ContainsKey(target.Id));
        builder.BuildSnapshotForSession(session, 181, 60, true);

        Assert.Equal(new[] { owner.Id }, session.LastSentEntities.Keys.ToArray());
    }

    [Fact]
    public void CappedKeyframeDoesNotPruneUnvisitedSentStates() {
        var world = new World();
        var owner = world.Spawn();
        for (var i = 0; i < 230; i++) {
            world.Spawn();
        }
        var session = NewSession(owner.Id);
        session.LastSentEntities[int.MaxValue] = new SentEntityState();
        var builder = new SnapshotBuilder(world);

        var packet = builder.BuildSnapshotForSession(session, 181, 60, true);

        Assert.Equal(220, packet.Entities.Count);
        Assert.True(session.LastSentEntities.ContainsKey(int.MaxValue));
        foreach (var entity in world.Entities.Where(entity => entity.Id != owner.Id).ToArray()) {
            world.Despawn(entity.Id);
        }
        builder.BuildSnapshotForSession(session, 182, 60, true);
        Assert.False(session.LastSentEntities.ContainsKey(int.MaxValue));
    }

    [Fact]
    public void ObservedEntitiesRefreshTtlEvenWhenFarthestTierBudgetSkipsThem() {
        var world = new World();
        var owner = world.Spawn();
        for (var i = 0; i < 32; i++) {
            world.Spawn(new Vector3(30, 0, 0));
        }
        var skipped = world.Spawn(new Vector3(30, 0, 0));
        var session = NewSession(owner.Id);
        session.LastSentEntities[skipped.Id] = new SentEntityState();

        var packet = new SnapshotBuilder(world).BuildSnapshotForSession(session, 181, 60, true);

        Assert.DoesNotContain(skipped.Id, Ids(packet));
        Assert.Equal(181L, session.LastSentEntities[skipped.Id].LastObservedTick);
        Assert.Equal(0L, session.LastSentEntities[skipped.Id].LastSentTick);
    }

    [Fact]
    public void SameTickObserverMovementRechecksDistanceAndChangesCandidateCell() {
        var world = new World();
        var owner = world.Spawn();
        var initiallyVisible = world.Spawn(new Vector3(44, 0, 0));
        var initiallyOutsideRadius = world.Spawn(new Vector3(46, 0, 0));
        var outsideOriginalCells = world.Spawn(new Vector3(139, 0, 0));
        var session = NewSession(owner.Id);
        var builder = new SnapshotBuilder(world);

        Assert.Equal(new[] { owner.Id, initiallyVisible.Id },
            Ids(builder.BuildSnapshotForSession(session, 1, 60, true)));
        owner.Position = new Vector3(2, 0, 0);
        session.SnapshotRoundRobinOffset = 0;
        Assert.Equal(new[] { owner.Id, initiallyVisible.Id, initiallyOutsideRadius.Id },
            Ids(builder.BuildSnapshotForSession(session, 1, 60, true)));
        owner.Position = new Vector3(100, 0, 0);
        Assert.Equal(new[] { owner.Id, outsideOriginalCells.Id },
            Ids(builder.BuildSnapshotForSession(session, 1, 60, true)));
    }

    [Fact]
    public void ObserversSharingCellDoNotShareDistanceFilteredResults() {
        var world = new World();
        var firstOwner = world.Spawn();
        var secondOwner = world.Spawn(new Vector3(19, 0, 0));
        var target = world.Spawn(new Vector3(46, 0, 0));
        var builder = new SnapshotBuilder(world);

        var first = builder.BuildSnapshotForSession(NewSession(firstOwner.Id), 1, 60, true);
        var second = builder.BuildSnapshotForSession(NewSession(secondOwner.Id), 1, 60, true);

        Assert.DoesNotContain(target.Id, Ids(first));
        Assert.Contains(target.Id, Ids(second));
        Assert.Equal(secondOwner.Id, second.Entities[0].EntityId);
    }

    [Fact]
    public void NextTickRebuildsGridAndCandidatesAfterMovementSpawnAndDespawn() {
        var world = new World();
        var owner = world.Spawn();
        var leaving = world.Spawn(new Vector3(1, 0, 0));
        var entering = world.Spawn(new Vector3(1000, 0, 0));
        var removed = world.Spawn(new Vector3(2, 0, 0));
        var builder = new SnapshotBuilder(world);
        Assert.Equal(new[] { owner.Id, leaving.Id, removed.Id },
            Ids(builder.BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true)));

        leaving.Position = new Vector3(1000, 0, 0);
        entering.Position = new Vector3(1, 0, 0);
        world.Despawn(removed.Id);
        var spawned = world.Spawn(new Vector3(2, 0, 0));
        var packet = builder.BuildSnapshotForSession(NewSession(owner.Id), 2, 60, true);

        Assert.Equal(new[] { owner.Id, entering.Id, spawned.Id }, Ids(packet));
    }

    [Fact]
    public void LongTravelDoesNotRetainHistoricalGridOrCandidateKeysAndReusesBucketLists() {
        var world = new World();
        var owner = world.Spawn();
        var companion = world.Spawn(new Vector3(1, 0, 0));
        var builder = new SnapshotBuilder(world);
        var session = NewSession(owner.Id);
        var buckets = new HashSet<object>(ReferenceEqualityComparer.Instance);
        object? previousCandidateKey = null;

        for (var tick = 1; tick <= 200; tick++) {
            owner.Position = new Vector3(tick * 100, 0, -tick * 100);
            companion.Position = owner.Position + Vector3.UnitX;
            session.SnapshotRoundRobinOffset = 0;
            Assert.Equal(new[] { owner.Id, companion.Id },
                Ids(builder.BuildSnapshotForSession(session, tick, 60, true)));

            var grid = DictionaryField(builder, "_interestGrid");
            var candidates = DictionaryField(builder, "_nearbyEntitiesByCell");
            Assert.Single(grid);
            Assert.Single(candidates);
            var bucket = Assert.Single(grid.Values.Cast<object>());
            Assert.Equal(2, Assert.IsAssignableFrom<ICollection>(bucket).Count);
            buckets.Add(bucket);
            if (previousCandidateKey != null) {
                Assert.False(candidates.Contains(previousCandidateKey!));
            }
            previousCandidateKey = Assert.Single(candidates.Keys.Cast<object>());
        }

        Assert.True(buckets.Count < 200, "Grid rebuilds should reuse bucket lists rather than allocate on every tick.");
    }

    [Fact]
    public void CandidateDictionaryDropsOtherObserverCellsOnEveryNewTick() {
        var world = new World();
        var firstOwner = world.Spawn();
        var secondOwner = world.Spawn(new Vector3(1000, 0, 0));
        var builder = new SnapshotBuilder(world);
        builder.BuildSnapshotForSession(NewSession(firstOwner.Id), 1, 60, true);
        builder.BuildSnapshotForSession(NewSession(secondOwner.Id), 1, 60, true);
        Assert.Equal(2, DictionaryField(builder, "_nearbyEntitiesByCell").Count);

        builder.BuildSnapshotForSession(NewSession(firstOwner.Id), 2, 60, true);

        Assert.Single(DictionaryField(builder, "_nearbyEntitiesByCell"));
    }

    [Fact]
    public void PublicPacketsOwnEntityObjectsAndListsButPreserveLiveComponentReferences() {
        var world = new World();
        var owner = world.Spawn(new Vector3(1, 2, 3));
        var oldVelocity = new VelocityComponent { X = 4 };
        var input = new InputComponent { LastSequence = 7 };
        owner.AddComponent(oldVelocity);
        owner.AddComponent(input);
        owner.AddComponent(new HealthComponent());
        var builder = new SnapshotBuilder(world);
        var session = NewSession(owner.Id);
        var retained = builder.BuildSnapshotForSession(session, 1, 60, true);
        var retainedEntity = Assert.Single(retained.Entities);
        Assert.Same(oldVelocity, Assert.Single(retainedEntity.Components));
        Assert.Equal(35, retainedEntity.EstimatedBytes);

        owner.Position = new Vector3(5, 6, 7);
        input.LastSequence = 9;
        owner.RemoveComponent<VelocityComponent>();
        var replacement = new VelocityComponent { Z = 8 };
        owner.AddComponent(replacement);
        var second = builder.BuildSnapshotForSession(session, 1, 60, true);
        var secondEntity = Assert.Single(second.Entities);

        Assert.NotSame(retained, second);
        Assert.NotSame(retained.Entities, second.Entities);
        Assert.NotSame(retainedEntity, secondEntity);
        Assert.NotSame(retainedEntity.Components, secondEntity.Components);
        Assert.Equal(new Vector3(1, 2, 3), Position(retainedEntity));
        Assert.Equal(7u, retainedEntity.LastInputSequence);
        Assert.Same(oldVelocity, Assert.Single(retainedEntity.Components));
        Assert.Equal(owner.Position, Position(secondEntity));
        Assert.Equal(9u, secondEntity.LastInputSequence);
        Assert.Same(replacement, Assert.Single(secondEntity.Components));

        oldVelocity.X = 10;
        Assert.Equal(10f, Assert.IsType<VelocityComponent>(retainedEntity.Components[0]).X);
        secondEntity.Components.Clear();
        second.Entities.Clear();
        var third = builder.BuildSnapshotForSession(session, 1, 60, true);
        Assert.Same(replacement, Assert.Single(Assert.Single(third.Entities).Components));
        Assert.Same(oldVelocity, Assert.Single(retainedEntity.Components));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BatchSerializationAndSessionStateMatchEquivalentPublicBuilds(bool force) {
        var world = new World();
        var firstOwner = world.Spawn();
        var secondOwner = world.Spawn(new Vector3(2, 0, 0));
        for (var i = 0; i < 260; i++) {
            var entity = world.Spawn(new Vector3(i < 220 ? i % 10 : -30, 0, 0));
            entity.AddComponent(new VelocityComponent { X = i % 2 });
            entity.AddComponent(new InputComponent { LastSequence = (uint)i });
        }
        var batchBuilder = new SnapshotBuilder(world);
        var publicBuilder = new SnapshotBuilder(world);
        var sessions = new[] { NewSession(firstOwner.Id), NewSession(secondOwner.Id) };
        sessions[0].SnapshotRoundRobinOffset = -7;
        sessions[1].SnapshotRoundRobinOffset = 125;
        foreach (var session in sessions) {
            foreach (var entity in world.Entities.Where(entity => entity.Id % 3 == 0)) {
                session.LastSentEntities[entity.Id] = new SentEntityState {
                    Position = entity.Position,
                    LastSentTick = 99,
                    LastObservedTick = 99,
                    LastFullSentTick = 90
                };
            }
            session.LastSentEntities[int.MaxValue] = new SentEntityState();
        }
        var referenceSessions = sessions.Select(CloneSession).ToArray();

        using var batch = BeginBatch(batchBuilder, 100);
        SnapshotPacket? previousBorrowed = null;
        for (var i = 0; i < sessions.Length; i++) {
            var expected = publicBuilder.BuildSnapshotForSession(referenceSessions[i], 100, 60, force);
            var actual = batch.Build(sessions[i], 60, force);
            Assert.Equal(Serialize(expected), Serialize(actual));
            Assert.Equal(expected.Entities.Select(e => e.EstimatedBytes).ToArray(),
                actual.Entities.Select(e => e.EstimatedBytes).ToArray());
            AssertSessionEqual(referenceSessions[i], sessions[i]);
            if (previousBorrowed != null) {
                Assert.Same(previousBorrowed, actual);
            }
            previousBorrowed = actual;
        }
    }

    [Fact]
    public void BatchCachesEntitySnapshotsButPublicBuildsInsideScopeRemainIndependent() {
        var world = new World();
        var owner = world.Spawn();
        var target = world.Spawn(Vector3.UnitX);
        target.AddComponent(new VelocityComponent { X = 1 });
        var builder = new SnapshotBuilder(world);
        SnapshotPacket retained;
        EntitySnapshot retainedTarget;
        byte[] retainedBytes;

        using (var batch = BeginBatch(builder, 1)) {
            var borrowed = batch.Build(NewSession(owner.Id), 60, true);
            var borrowedTarget = borrowed.Entities.Single(e => e.EntityId == target.Id);
            retained = builder.BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true);
            retainedTarget = retained.Entities.Single(e => e.EntityId == target.Id);
            retainedBytes = Serialize(retained);
            Assert.NotSame(borrowed, retained);
            Assert.NotSame(borrowed.Entities, retained.Entities);
            Assert.NotSame(borrowedTarget, retainedTarget);
            Assert.NotSame(borrowedTarget.Components, retainedTarget.Components);

            var anotherPublic = builder.BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true);
            Assert.NotSame(retainedTarget, anotherPublic.Entities.Single(e => e.EntityId == target.Id));
            Assert.NotSame(retainedTarget.Components,
                anotherPublic.Entities.Single(e => e.EntityId == target.Id).Components);
            var nextBorrowed = batch.Build(NewSession(target.Id), 60, true);
            Assert.Same(borrowed, nextBorrowed);
            Assert.Same(borrowedTarget, nextBorrowed.Entities[0]);
            Assert.Equal(retainedBytes, Serialize(retained));
        }

        target.Position = new Vector3(10, 20, 30);
        target.RemoveComponent<VelocityComponent>();
        using (var batch = BeginBatch(builder, 2)) {
            batch.Build(NewSession(target.Id), 60, true);
        }

        Assert.Equal(Vector3.UnitX, Position(retainedTarget));
        Assert.Single(retainedTarget.Components);
        Assert.Equal(retainedBytes, Serialize(retained));
    }

    [Fact]
    public void BatchClearsBorrowedPacketForMissingOwnerWithoutChangingPublicPacket() {
        var world = new World();
        var owner = world.Spawn();
        var builder = new SnapshotBuilder(world);
        var retained = builder.BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true);
        using var batch = BeginBatch(builder, 1);
        var borrowed = batch.Build(NewSession(owner.Id), 60, true);
        Assert.Single(borrowed.Entities);

        var empty = batch.Build(NewSession(-1), 60, true);

        Assert.Same(borrowed, empty);
        Assert.Empty(empty.Entities);
        Assert.Equal(owner.Id, Assert.Single(retained.Entities).EntityId);
        Assert.Equal(owner.Id, Assert.Single(batch.Build(NewSession(owner.Id), 60, true).Entities).EntityId);
    }

    [Fact]
    public void NewBatchAtSameTickPreservesOncePerTickGridMembership() {
        var world = new World();
        var owner = world.Spawn();
        var leaving = world.Spawn(Vector3.UnitX);
        var entering = world.Spawn(new Vector3(1000, 0, 0));
        var builder = new SnapshotBuilder(world);
        var publicBuilder = new SnapshotBuilder(world);
        publicBuilder.BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true);
        using (var batch = BeginBatch(builder, 1)) {
            Assert.Equal(new[] { owner.Id, leaving.Id }, Ids(batch.Build(NewSession(owner.Id), 60, true)));
        }

        leaving.Position = new Vector3(1000, 0, 0);
        entering.Position = Vector3.UnitX;
        using (var batch = BeginBatch(builder, 1)) {
            var actual = Serialize(batch.Build(NewSession(owner.Id), 60, true));
            var expected = publicBuilder.BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true);
            Assert.Equal(new[] { owner.Id }, Ids(expected));
            Assert.Equal(Serialize(expected), actual);
        }
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(2L)]
    public void NewBatchRefreshesPositionComponentMembershipAndInputEvenAtSameTick(long nextTick) {
        var world = new World();
        var owner = world.Spawn(new Vector3(1, 2, 3));
        var oldVelocity = new VelocityComponent { X = 4 };
        var input = new InputComponent { LastSequence = 7 };
        owner.AddComponent(oldVelocity);
        owner.AddComponent(input);
        var builder = new SnapshotBuilder(world);
        SnapshotPacket retained;
        using (var batch = BeginBatch(builder, 1)) {
            Assert.Equal(Serialize(builder.BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true)),
                Serialize(batch.Build(NewSession(owner.Id), 60, true)));
            retained = builder.BuildSnapshotForSession(NewSession(owner.Id), 1, 60, true);
        }

        owner.Position = new Vector3(100, 200, 300);
        input.LastSequence = 11;
        owner.RemoveComponent<VelocityComponent>();
        oldVelocity.X = 6;
        using (var batch = BeginBatch(builder, nextTick)) {
            var packet = batch.Build(NewSession(owner.Id), 60, true);
            var entity = Assert.Single(packet.Entities);
            Assert.Equal(owner.Position, Position(entity));
            Assert.Equal(11u, entity.LastInputSequence);
            Assert.Empty(entity.Components);
            Assert.Equal(22, entity.EstimatedBytes);
            Assert.Equal(Serialize(builder.BuildSnapshotForSession(NewSession(owner.Id), nextTick, 60, true)),
                Serialize(packet));
        }

        owner.RemoveComponent<InputComponent>();
        var newVelocity = new VelocityComponent { Z = 9 };
        owner.AddComponent(newVelocity);
        using (var batch = BeginBatch(builder, nextTick)) {
            var entity = Assert.Single(batch.Build(NewSession(owner.Id), 60, true).Entities);
            Assert.Equal(0u, entity.LastInputSequence);
            Assert.Equal(35, entity.EstimatedBytes);
            Assert.Same(newVelocity, Assert.Single(entity.Components));
        }

        var retainedEntity = Assert.Single(retained.Entities);
        Assert.Equal(new Vector3(1, 2, 3), Position(retainedEntity));
        Assert.Equal(7u, retainedEntity.LastInputSequence);
        Assert.Equal(35, retainedEntity.EstimatedBytes);
        Assert.Same(oldVelocity, Assert.Single(retainedEntity.Components));
        Assert.Equal(6f, oldVelocity.X);
    }

    [Fact]
    public void BatchRejectsNestedAndDisposedUseAndStaleDisposeCannotEndNewScope() {
        var world = new World();
        var owner = world.Spawn();
        owner.AddComponent(new VelocityComponent { X = 1 });
        var builder = new SnapshotBuilder(world);
        var session = NewSession(owner.Id);
        var first = BeginBatch(builder, 1);
        var borrowed = first.Build(session, 60, true);
        var borrowedEntity = Assert.Single(borrowed.Entities);
        var nested = Assert.Throws<TargetInvocationException>(() => BeginBatch(builder, 1));
        Assert.IsType<InvalidOperationException>(nested.InnerException);
        first.Dispose();
        Assert.Empty(borrowed.Entities);
        Assert.Empty(borrowedEntity.Components);
        Assert.Empty(DictionaryField(builder, "_broadcastEntities"));
        var disposed = Assert.Throws<TargetInvocationException>(() => first.Build(session, 60, true));
        Assert.IsType<ObjectDisposedException>(disposed.InnerException);

        using var second = BeginBatch(builder, 2);
        first.Dispose();
        Assert.Single(second.Build(session, 60, true).Entities);
        var stale = Assert.Throws<TargetInvocationException>(() => first.Build(session, 60, true));
        Assert.IsType<ObjectDisposedException>(stale.InnerException);
    }

    [Fact]
    public void ExceptionalBatchExitReleasesReferencesAndAllowsNextBatch() {
        var world = new World();
        var owner = world.Spawn();
        owner.AddComponent(new VelocityComponent());
        var builder = new SnapshotBuilder(world);
        SnapshotPacket? borrowed = null;
        Assert.Throws<InvalidOperationException>((Action)(() => {
            using var batch = BeginBatch(builder, 1);
            borrowed = batch.Build(NewSession(owner.Id), 60, true);
            throw new InvalidOperationException("Simulated synchronous send failure.");
        }));

        Assert.NotNull(borrowed);
        Assert.Empty(borrowed.Entities);
        Assert.Empty(DictionaryField(builder, "_broadcastEntities"));
        using var next = BeginBatch(builder, 1);
        Assert.Single(Assert.Single(next.Build(NewSession(owner.Id), 60, true).Entities).Components);
    }

    private static PlayerSession NewSession(int entityId) =>
        new((NetPeer)RuntimeHelpers.GetUninitializedObject(typeof(NetPeer)), 0) { EntityId = entityId };

    private static int[] Ids(SnapshotPacket packet) => packet.Entities.Select(entity => entity.EntityId).ToArray();

    private static Vector3 Position(EntitySnapshot entity) => new(entity.PositionX, entity.PositionY, entity.PositionZ);

    private static byte[] Serialize(SnapshotPacket packet) {
        var writer = new NetDataWriter();
        packet.Serialize(writer);
        return writer.CopyData();
    }

    private static IDictionary DictionaryField(SnapshotBuilder builder, string name) {
        var field = typeof(SnapshotBuilder).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<IDictionary>(field.GetValue(builder));
    }

    private static PlayerSession CloneSession(PlayerSession original) {
        var clone = NewSession(original.EntityId);
        clone.SnapshotRoundRobinOffset = original.SnapshotRoundRobinOffset;
        foreach (var (id, state) in original.LastSentEntities) {
            clone.LastSentEntities[id] = new SentEntityState {
                Position = state.Position,
                Velocity = state.Velocity,
                LastSentTick = state.LastSentTick,
                LastObservedTick = state.LastObservedTick,
                LastFullSentTick = state.LastFullSentTick
            };
        }
        return clone;
    }

    private static void AssertSessionEqual(PlayerSession expected, PlayerSession actual) {
        Assert.Equal(expected.SnapshotRoundRobinOffset, actual.SnapshotRoundRobinOffset);
        Assert.Equal(expected.LastSentEntities.Keys.OrderBy(id => id).ToArray(),
            actual.LastSentEntities.Keys.OrderBy(id => id).ToArray());
        foreach (var (id, expectedState) in expected.LastSentEntities) {
            var actualState = actual.LastSentEntities[id];
            Assert.Equal(expectedState.Position, actualState.Position);
            Assert.Equal(expectedState.Velocity, actualState.Velocity);
            Assert.Equal(expectedState.LastSentTick, actualState.LastSentTick);
            Assert.Equal(expectedState.LastObservedTick, actualState.LastObservedTick);
            Assert.Equal(expectedState.LastFullSentTick, actualState.LastFullSentTick);
        }
    }

    private static ReflectedBatch BeginBatch(SnapshotBuilder builder, long tick) {
        var begin = typeof(SnapshotBuilder).GetMethod("BeginBroadcastBatch",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(begin);
        Assert.True(begin.ReturnType.IsValueType);
        var scope = begin.Invoke(builder, new object[] { tick });
        Assert.NotNull(scope);
        return new ReflectedBatch(scope);
    }

    private sealed class ReflectedBatch(object scope) : IDisposable {
        public SnapshotPacket Build(PlayerSession session, int tickRate, bool force) {
            var method = scope.GetType().GetMethod("Build", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method);
            return Assert.IsType<SnapshotPacket>(method.Invoke(scope, new object[] { session, tickRate, force }));
        }

        public void Dispose() => Assert.IsAssignableFrom<IDisposable>(scope).Dispose();
    }
}
