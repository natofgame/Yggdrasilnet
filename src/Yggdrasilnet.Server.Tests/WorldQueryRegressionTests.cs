using System.Numerics;
using Yggdrasilnet.Server.Simulation.World;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Tests;

public sealed class WorldQueryRegressionTests {
    [Fact]
    public void StandaloneEntityUsesExactRuntimeTypesAndReplacementIdentity() {
        var entity = new Entity(42, new Vector3(1f, 2f, 3f));
        BaseComponent original = new DerivedComponent();
        entity.AddComponent(original);

        Assert.Equal(42, entity.Id);
        Assert.Equal(new Vector3(1f, 2f, 3f), entity.Position);
        Assert.True(entity.HasComponent<DerivedComponent>());
        Assert.True(entity.TryGetComponent<DerivedComponent>(out var derived));
        Assert.Same(original, derived);
        Assert.False(entity.HasComponent<BaseComponent>());
        Assert.False(entity.HasComponent<ITaggedComponent>());
        Assert.False(entity.HasComponent<IComponent>());
        Assert.False(entity.TryGetComponent<BaseComponent>(out var missingBase));
        Assert.Null(missingBase);
        Assert.False(entity.TryGetComponent<ITaggedComponent>(out var missingInterface));
        Assert.Null(missingInterface);
        Assert.False(entity.RemoveComponent<BaseComponent>());
        Assert.False(entity.RemoveComponent<ITaggedComponent>());
        Assert.False(entity.RemoveComponent<IComponent>());

        ITaggedComponent replacement = new DerivedComponent();
        entity.AddComponent(replacement);
        Assert.Same(replacement, Assert.Single(entity.Components));
        Assert.True(entity.TryGetComponent<DerivedComponent>(out derived));
        Assert.Same(replacement, derived);

        var exactBase = new BaseComponent();
        entity.AddComponent(exactBase);
        Assert.Equal(2, entity.Components.Count());
        Assert.True(entity.TryGetComponent<BaseComponent>(out var foundBase));
        Assert.Same(exactBase, foundBase);
        Assert.True(entity.RemoveComponent<BaseComponent>());
        Assert.False(entity.RemoveComponent<BaseComponent>());
        Assert.True(entity.HasComponent<DerivedComponent>());
        Assert.True(entity.RemoveComponent<DerivedComponent>());
        Assert.False(entity.RemoveComponent<DerivedComponent>());
        Assert.False(entity.TryGetComponent<DerivedComponent>(out derived));
        Assert.Null(derived);
        Assert.Empty(entity.Components);
    }

    [Fact]
    public void DespawnedAndStandaloneEntitiesRemainFunctionalWithoutAffectingWorld() {
        var world = CreateWorld(false, out var entities);
        var detached = entities[0];
        Warm(world);
        Assert.True(world.Despawn(detached.Id));
        Assert.False(world.Despawn(detached.Id));
        Assert.False(world.TryGetEntity(detached.Id, out var missing));
        Assert.Null(missing);
        Warm(world);

        var standalone = new Entity(entities[6].Id);
        foreach (var entity in new[] { detached, standalone }) {
            var component = new MarkerComponent(100);
            entity.AddComponent(component);
            Assert.True(entity.TryGetComponent<MarkerComponent>(out var found));
            Assert.Same(component, found);
            Assert.True(entity.RemoveComponent<MarkerComponent>());
            Assert.False(entity.RemoveComponent<MarkerComponent>());
            entity.AddComponent(component);
            entity.Position = new Vector3(7f, 8f, 9f);
            Assert.Equal(new Vector3(7f, 8f, 9f), entity.Position);
            Assert.Same(component, Assert.Single(entity.Components));
        }

        Assert.True(world.TryGetEntity(entities[6].Id, out var live));
        Assert.Same(entities[6], live);
        Assert.Equal(new[] { entities[6], entities[11] },
            world.Query<MarkerComponent>().Select(pair => pair.Entity).ToArray());
        AssertMatchesReference<MarkerComponent>(world);
    }

    [Fact]
    public void QueriesDoNotMatchAssignableBaseOrInterfaceTypes() {
        var world = CreateWorld(false, out var entities);
        BaseComponent derived = new DerivedComponent();
        entities[3].AddComponent(derived);

        for (var pass = 0; pass < 2; pass++) {
            Assert.Empty(world.Query<BaseComponent>());
            Assert.Empty(world.Query<ITaggedComponent>());
            Assert.Empty(world.Query<IComponent>());
            Assert.Same(derived, Assert.Single(world.Query<DerivedComponent>()).Component);
        }

        var exactBase = new BaseComponent();
        entities[3].AddComponent(exactBase);
        Assert.Same(exactBase, Assert.Single(world.Query<BaseComponent>()).Component);
        Assert.Same(derived, Assert.Single(world.Query<DerivedComponent>()).Component);
        Assert.Empty(world.Query<ITaggedComponent>());
        Assert.Empty(world.Query<IComponent>());
        Assert.True(entities[3].RemoveComponent<BaseComponent>());
        Assert.Empty(world.Query<BaseComponent>());
        Assert.Same(derived, Assert.Single(world.Query<DerivedComponent>()).Component);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueryAndGetEnumeratorAreLazyUntilFirstMoveNext(bool dense) {
        var world = CreateWorld(dense, out var entities);
        Warm(world);
        var query = world.Query<MarkerComponent>();
        using var actual = query.GetEnumerator();
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();

        Assert.True(world.Despawn(entities[0].Id));
        entities[3].AddComponent(new MarkerComponent(30));
        var spawned = world.Spawn();
        spawned.AddComponent(new MarkerComponent(40));

        AssertRemainderMatches(expected, actual);
        AssertMatchesReference<MarkerComponent>(world);
    }

    [Fact]
    public void ReenumerationSeesLatestWorldButExhaustedEnumeratorStaysExhausted() {
        var world = CreateWorld(false, out var entities);
        var query = world.Query<MarkerComponent>();
        Warm(world);
        using var exhausted = query.GetEnumerator();
        while (exhausted.MoveNext()) { }

        Assert.True(world.Despawn(entities[0].Id));
        Assert.True(entities[6].RemoveComponent<MarkerComponent>());
        entities[3].AddComponent(new MarkerComponent(30));
        world.Spawn().AddComponent(new MarkerComponent(40));

        Assert.False(exhausted.MoveNext());
        Assert.False(exhausted.MoveNext());
        AssertSameResults(ReferenceQuery<MarkerComponent>(world), query);
        AssertSameResults(ReferenceQuery<MarkerComponent>(world), query);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarmQueriesPreserveDictionaryOrderAfterMultipleFreeSlotReuses(bool dense) {
        var world = CreateWorld(dense, out var entities);
        Warm(world);
        Assert.True(world.Despawn(entities[2].Id));
        Assert.True(world.Despawn(entities[7].Id));
        Assert.True(world.Despawn(entities[0].Id));
        var first = world.Spawn();
        var second = world.Spawn();
        var third = world.Spawn();
        first.AddComponent(new MarkerComponent(100));
        second.AddComponent(new MarkerComponent(200));
        third.AddComponent(new MarkerComponent(300));
        Warm(world);

        Assert.True(world.Despawn(second.Id));
        Assert.True(world.Despawn(entities[5].Id));
        world.Spawn().AddComponent(new MarkerComponent(400));
        world.Spawn().AddComponent(new MarkerComponent(500));
        Warm(world);
        AssertMatchesReference<MarkerComponent>(world);
    }

    [Fact]
    public void WarmEmptyQueriesObserveComponentAndEntityAdditions() {
        var world = CreateWorld(false, out var entities);
        foreach (var entity in entities) {
            entity.RemoveComponent<MarkerComponent>();
        }
        Warm(world);
        Assert.Empty(world.Query<MarkerComponent>());
        var first = new MarkerComponent(1);
        entities[8].AddComponent(first);
        Assert.Same(first, Assert.Single(world.Query<MarkerComponent>()).Component);
        Warm(world);
        Assert.True(entities[8].RemoveComponent<MarkerComponent>());
        Warm(world);
        Assert.Empty(world.Query<MarkerComponent>());

        var second = new MarkerComponent(2);
        world.Spawn().AddComponent(second);
        Assert.Same(second, Assert.Single(world.Query<MarkerComponent>()).Component);
        Warm(world);
    }

    [Fact]
    public void EmptyQueryEnumeratorIsLazyAndRemainsExhaustedAfterCompletion() {
        var world = new World();
        Warm(world);
        var query = world.Query<MarkerComponent>();
        using var lazy = query.GetEnumerator();
        var entity = world.Spawn();
        var component = new MarkerComponent(1);
        entity.AddComponent(component);
        Assert.True(lazy.MoveNext());
        Assert.Same(entity, lazy.Current.Entity);
        Assert.Same(component, lazy.Current.Component);
        Assert.False(lazy.MoveNext());

        Assert.True(world.Despawn(entity.Id));
        Warm(world);
        using var empty = query.GetEnumerator();
        Assert.False(empty.MoveNext());
        world.Spawn().AddComponent(new MarkerComponent(2));
        Assert.False(empty.MoveNext());
        Assert.False(lazy.MoveNext());
        Assert.Single(query);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveQuerySeesLaterComponentAddsAndRemoves(bool dense) {
        var world = CreateWorld(dense, out var entities);
        entities[3].RemoveComponent<MarkerComponent>();
        Warm(world);
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var actual = world.Query<MarkerComponent>().GetEnumerator();
        Assert.True(AssertNextMatches(expected, actual));
        entities[3].AddComponent(new MarkerComponent(30));
        Assert.True(entities[6].RemoveComponent<MarkerComponent>());
        Assert.True(entities[11].RemoveComponent<MarkerComponent>());
        AssertRemainderMatches(expected, actual);
        Warm(world);
    }

    [Fact]
    public void ActiveSparseQuerySeesNewMatchBeyondLastCachedPosition() {
        var world = CreateWorld(false, out var entities);
        Warm(world);
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var actual = world.Query<MarkerComponent>().GetEnumerator();
        Assert.True(AssertNextMatches(expected, actual));
        Assert.True(AssertNextMatches(expected, actual));
        Assert.True(AssertNextMatches(expected, actual));
        Assert.Same(entities[11], actual.Current.Entity);

        var added = new MarkerComponent(14);
        entities[14].AddComponent(added);
        Warm(world);
        Assert.True(AssertNextMatches(expected, actual));
        Assert.Same(entities[14], actual.Current.Entity);
        Assert.Same(added, actual.Current.Component);
        Assert.False(AssertNextMatches(expected, actual));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveQueryReadsReplacementIdentityWithoutChangingAlreadyYieldedTuple(bool dense) {
        var world = CreateWorld(dense, out var entities);
        Warm(world);
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var actual = world.Query<MarkerComponent>().GetEnumerator();
        Assert.True(AssertNextMatches(expected, actual));
        var yielded = actual.Current;
        var currentReplacement = new MarkerComponent(100);
        var futureReplacement = new MarkerComponent(600);
        yielded.Entity.AddComponent(currentReplacement);
        entities[6].AddComponent(futureReplacement);
        Assert.Same(yielded.Component, actual.Current.Component);
        Assert.NotSame(currentReplacement, actual.Current.Component);
        AssertRemainderMatches(expected, actual);
        Assert.Same(futureReplacement,
            world.Query<MarkerComponent>().Single(pair => pair.Entity == entities[6]).Component);
        Warm(world);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComponentChangesBehindCursorDoNotRevisitEntities(bool dense) {
        var world = CreateWorld(dense, out var entities);
        entities[0].RemoveComponent<MarkerComponent>();
        entities[1].AddComponent(new MarkerComponent(1));
        Warm(world);
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var actual = world.Query<MarkerComponent>().GetEnumerator();
        Assert.True(AssertNextMatches(expected, actual));
        Assert.Same(entities[1], actual.Current.Entity);
        var yielded = actual.Current.Component;
        entities[0].AddComponent(new MarkerComponent(0));
        Assert.True(entities[1].RemoveComponent<MarkerComponent>());
        entities[1].AddComponent(new MarkerComponent(10));
        Assert.Same(yielded, actual.Current.Component);
        AssertRemainderMatches(expected, actual);
        Warm(world);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void ActiveQueryAllowsDespawnOfPreviousCurrentOrFutureEntity(bool dense, int target) {
        var world = CreateWorld(dense, out var entities);
        Warm(world);
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var actual = world.Query<MarkerComponent>().GetEnumerator();
        Assert.True(AssertNextMatches(expected, actual));
        var previous = actual.Current.Entity;
        Assert.True(AssertNextMatches(expected, actual));
        var current = actual.Current.Entity;
        var removed = target == 0 ? previous : target == 1 ? current : entities[11];
        Assert.True(world.Despawn(removed.Id));
        removed.AddComponent(new MarkerComponent(999));
        AssertRemainderMatches(expected, actual);
        Warm(world);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveQueryCanFinishAfterAllRemainingEntitiesAreDespawned(bool dense) {
        var world = CreateWorld(dense, out var entities);
        Warm(world);
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var actual = world.Query<MarkerComponent>().GetEnumerator();
        Assert.True(AssertNextMatches(expected, actual));
        foreach (var entity in entities) {
            Assert.True(world.Despawn(entity.Id));
        }
        Assert.False(AssertNextMatches(expected, actual));
        Assert.Empty(world.Query<MarkerComponent>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SpawnInvalidatesActiveQueryEvenAfterLastYield(bool dense, bool afterLastYield) {
        var world = CreateWorld(dense, out _);
        Warm(world);
        var yieldCount = afterLastYield ? world.Query<MarkerComponent>().Count() : 1;
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var actual = world.Query<MarkerComponent>().GetEnumerator();
        for (var i = 0; i < yieldCount; i++) {
            Assert.True(AssertNextMatches(expected, actual));
        }

        world.Spawn();
        Assert.Throws<InvalidOperationException>(() => { expected.MoveNext(); });
        Assert.Throws<InvalidOperationException>(() => { actual.MoveNext(); });
        Warm(world);
    }

    [Fact]
    public void SpawnAfterLastSparseMatchInvalidatesBeforeScanningTrailingNonmatches() {
        var world = CreateWorld(false, out var entities);
        Assert.True(entities[11].RemoveComponent<MarkerComponent>());
        Warm(world);
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var actual = world.Query<MarkerComponent>().GetEnumerator();
        Assert.True(AssertNextMatches(expected, actual));
        Assert.True(AssertNextMatches(expected, actual));
        Assert.Same(entities[6], actual.Current.Entity);
        world.Spawn().AddComponent(new MarkerComponent(100));
        Assert.Throws<InvalidOperationException>(() => { expected.MoveNext(); });
        Assert.Throws<InvalidOperationException>(() => { actual.MoveNext(); });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DespawnThenSpawnInvalidatesEvenWhenEntityCountIsUnchanged(bool dense) {
        var world = CreateWorld(dense, out var entities);
        Warm(world);
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var actual = world.Query<MarkerComponent>().GetEnumerator();
        Assert.True(AssertNextMatches(expected, actual));
        Assert.True(world.Despawn(entities[6].Id));
        world.Spawn().AddComponent(new MarkerComponent(600));
        Assert.Equal(entities.Length, world.Entities.Count);
        Warm(world);
        Assert.Throws<InvalidOperationException>(() => { expected.MoveNext(); });
        Assert.Throws<InvalidOperationException>(() => { actual.MoveNext(); });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedRemovalsAndUnrelatedComponentsDoNotInvalidateActiveQuery(bool dense) {
        var world = CreateWorld(dense, out var entities);
        Warm(world);
        using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var actual = world.Query<MarkerComponent>().GetEnumerator();
        Assert.True(AssertNextMatches(expected, actual));
        Assert.False(world.Despawn(-1));
        Assert.False(entities[6].RemoveComponent<OtherComponent>());
        entities[6].AddComponent(new OtherComponent());
        entities[6].AddComponent(new OtherComponent());
        Assert.True(entities[6].RemoveComponent<OtherComponent>());
        Assert.False(entities[6].RemoveComponent<OtherComponent>());
        AssertRemainderMatches(expected, actual);
        Warm(world);
    }

    [Fact]
    public void InterleavedSparseQueriesKeepIndependentCursorsAcrossNewCacheBuilds() {
        var world = CreateWorld(false, out var entities);
        Warm(world);
        var query = world.Query<MarkerComponent>();
        using var first = query.GetEnumerator();
        using var second = query.GetEnumerator();
        using var firstExpected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var secondExpected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        Assert.True(AssertNextMatches(firstExpected, first));
        Assert.True(AssertNextMatches(secondExpected, second));
        Assert.True(AssertNextMatches(secondExpected, second));

        Assert.True(entities[6].RemoveComponent<MarkerComponent>());
        Assert.True(entities[11].RemoveComponent<MarkerComponent>());
        entities[3].AddComponent(new MarkerComponent(3));
        entities[9].AddComponent(new MarkerComponent(9));
        Warm(world);
        Assert.True(AssertNextMatches(firstExpected, first));
        Assert.Same(entities[3], first.Current.Entity);
        Assert.True(AssertNextMatches(secondExpected, second));
        Assert.Same(entities[9], second.Current.Entity);

        Assert.True(entities[9].RemoveComponent<MarkerComponent>());
        entities[10].AddComponent(new MarkerComponent(10));
        Warm(world);
        Assert.True(AssertNextMatches(firstExpected, first));
        Assert.True(AssertNextMatches(secondExpected, second));
        Assert.Same(entities[10], first.Current.Entity);
        Assert.Same(entities[10], second.Current.Entity);
        Assert.False(AssertNextMatches(firstExpected, first));
        Assert.False(AssertNextMatches(secondExpected, second));
    }

    [Fact]
    public void DifferentComponentQueriesRemainIndependentDuringMutation() {
        var world = CreateWorld(false, out var entities);
        entities[2].AddComponent(new OtherComponent());
        entities[8].AddComponent(new OtherComponent());
        Warm(world);
        AssertMatchesReference<OtherComponent>(world);
        AssertMatchesReference<OtherComponent>(world);
        using var markers = world.Query<MarkerComponent>().GetEnumerator();
        using var others = world.Query<OtherComponent>().GetEnumerator();
        using var expectedMarkers = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
        using var expectedOthers = ReferenceQuery<OtherComponent>(world).GetEnumerator();
        Assert.True(AssertNextMatches(expectedMarkers, markers));
        Assert.True(AssertNextMatches(expectedOthers, others));

        Assert.True(entities[6].RemoveComponent<MarkerComponent>());
        entities[3].AddComponent(new MarkerComponent(3));
        Assert.True(entities[8].RemoveComponent<OtherComponent>());
        entities[7].AddComponent(new OtherComponent());
        Warm(world);
        AssertMatchesReference<OtherComponent>(world);
        AssertRemainderMatches(expectedMarkers, markers);
        AssertRemainderMatches(expectedOthers, others);
    }

    [Fact]
    public void OlderActiveQueriesStayInvalidatedAfterNewQueryIsBuiltFollowingSpawn() {
        var world = CreateWorld(false, out _);
        Warm(world);
        using var first = world.Query<MarkerComponent>().GetEnumerator();
        using var second = world.Query<MarkerComponent>().GetEnumerator();
        Assert.True(first.MoveNext());
        Assert.True(second.MoveNext());
        Assert.True(second.MoveNext());
        world.Spawn().AddComponent(new MarkerComponent(100));
        Warm(world);
        Assert.Throws<InvalidOperationException>(() => { first.MoveNext(); });
        Assert.Throws<InvalidOperationException>(() => { second.MoveNext(); });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeterministicMutationTraceMatchesOriginalEnumeration(bool dense) {
        var world = CreateWorld(dense, out var entities);
        var knownEntities = entities.ToList();
        var random = new Random(71319);
        for (var step = 0; step < 120; step++) {
            Warm(world);
            AssertMatchesReference<OtherComponent>(world);
            using var expected = ReferenceQuery<MarkerComponent>(world).GetEnumerator();
            using var actual = world.Query<MarkerComponent>().GetEnumerator();
            var advances = random.Next(4);
            for (var i = 0; i < advances; i++) {
                AssertNextMatches(expected, actual);
            }

            var entity = knownEntities[random.Next(knownEntities.Count)];
            switch (random.Next(8)) {
                case 0:
                    var spawned = world.Spawn();
                    knownEntities.Add(spawned);
                    if (random.Next(2) == 0) {
                        spawned.AddComponent(new MarkerComponent(step));
                    }
                    break;
                case 1:
                    world.Despawn(entity.Id);
                    break;
                case 2:
                case 3:
                    entity.AddComponent(new MarkerComponent(step));
                    break;
                case 4:
                    entity.RemoveComponent<MarkerComponent>();
                    break;
                case 5:
                    entity.AddComponent(new OtherComponent());
                    break;
                case 6:
                    entity.RemoveComponent<OtherComponent>();
                    Assert.False(world.Despawn(-1));
                    break;
                case 7:
                    world.Despawn(entity.Id);
                    knownEntities.Add(world.Spawn());
                    break;
            }

            // A newer query must not repair an older cursor's invalidated state.
            Warm(world);
            AssertRemainderMatches(expected, actual);
            AssertMatchesReference<OtherComponent>(world);
        }
    }

    private static World CreateWorld(bool dense, out Entity[] entities) {
        var world = new World();
        entities = new Entity[15];
        for (var i = 0; i < entities.Length; i++) {
            entities[i] = world.Spawn();
            if (dense || i == 0 || i == 6 || i == 11) {
                entities[i].AddComponent(new MarkerComponent(i));
            }
        }
        return world;
    }

    private static void Warm(World world) {
        AssertMatchesReference<MarkerComponent>(world);
        AssertMatchesReference<MarkerComponent>(world);
    }

    // This is the original query implementation, intentionally using the live dictionary view.
    private static IEnumerable<(Entity Entity, T Component)> ReferenceQuery<T>(World world)
        where T : class, IComponent {
        foreach (var entity in world.Entities) {
            if (entity.TryGetComponent<T>(out var component)) {
                yield return (entity, component);
            }
        }
    }

    private static void AssertMatchesReference<T>(World world) where T : class, IComponent {
        AssertSameResults(ReferenceQuery<T>(world), world.Query<T>());
    }

    private static void AssertSameResults<T>(
        IEnumerable<(Entity Entity, T Component)> expected,
        IEnumerable<(Entity Entity, T Component)> actual) where T : class, IComponent {
        var expectedResults = expected.ToArray();
        var actualResults = actual.ToArray();
        Assert.Equal(expectedResults.Length, actualResults.Length);
        for (var i = 0; i < expectedResults.Length; i++) {
            Assert.Same(expectedResults[i].Entity, actualResults[i].Entity);
            Assert.Same(expectedResults[i].Component, actualResults[i].Component);
        }
    }

    private static bool AssertNextMatches<T>(
        IEnumerator<(Entity Entity, T Component)> expected,
        IEnumerator<(Entity Entity, T Component)> actual) where T : class, IComponent {
        var expectedMoved = false;
        var actualMoved = false;
        var expectedException = Record.Exception(() => { expectedMoved = expected.MoveNext(); });
        var actualException = Record.Exception(() => { actualMoved = actual.MoveNext(); });
        Assert.Equal(expectedException?.GetType(), actualException?.GetType());
        if (expectedException is not null) {
            Assert.IsType<InvalidOperationException>(expectedException);
            return false;
        }
        Assert.Equal(expectedMoved, actualMoved);
        if (expectedMoved) {
            Assert.Same(expected.Current.Entity, actual.Current.Entity);
            Assert.Same(expected.Current.Component, actual.Current.Component);
        }
        return expectedMoved;
    }

    private static void AssertRemainderMatches<T>(
        IEnumerator<(Entity Entity, T Component)> expected,
        IEnumerator<(Entity Entity, T Component)> actual) where T : class, IComponent {
        while (AssertNextMatches(expected, actual)) { }
    }

    private sealed class MarkerComponent(int value) : IComponent {
        public int Value { get; } = value;
    }

    private sealed class OtherComponent : IComponent { }

    private interface ITaggedComponent : IComponent { }

    private class BaseComponent : ITaggedComponent { }

    private sealed class DerivedComponent : BaseComponent { }
}
