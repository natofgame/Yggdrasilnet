using System.Collections;
using System.Numerics;
using System.Reflection;
using Yggdrasilnet.Server.Simulation.World;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Simulation.World.System;
using Yggdrasilnet.Server.Simulation.World.System.Steering;

namespace Yggdrasilnet.Server.Tests;

public sealed class SteeringPerformanceRegressionTests {
    private const float DeltaTime = 1f / 30f;

    [Theory]
    [InlineData(0f, 1f, false)]
    [InlineData(-2f, 1f, false)]
    [InlineData(2f, 0f, false)]
    [InlineData(0f, 1f, true)]
    [InlineData(-2f, 1f, true)]
    [InlineData(2f, 0f, true)]
    public void DisabledAvoidanceDoesNotPopulateTheSteeringGrid(float radius, float weight, bool hasPlayer) {
        var world = new World();
        var system = new SteeringSystem();
        var agent = SpawnAgent(world, Vector2.Zero, radius, weight);
        SpawnAgent(world, new Vector2(0.5f, 0f), radius, weight);
        if (hasPlayer) {
            SpawnPlayer(world, new Vector2(1f, 0f));
        }

        system.Update(world, DeltaTime);

        AssertGrid(system, "_steeringGrid", 1f, Array.Empty<Entity>());
        AssertPool(system, "_steeringBuckets", 0);
        AssertVelocity(agent, Vector2.Zero);
    }

    [Theory]
    [InlineData(1f, -0.75f)]
    [InlineData(-1f, 0.75f)]
    public void DisabledAgentsRemainNeighborsAndNegativeWeightsRemainActive(float weight, float expectedX) {
        var world = new World();
        var system = new SteeringSystem();
        var active = SpawnAgent(world, Vector2.Zero, 2f, weight);
        var disabled = SpawnAgent(world, new Vector2(0.5f, 0f), 2f, 0f);

        system.Update(world, DeltaTime);

        AssertGrid(system, "_steeringGrid", 2f, new[] { active, disabled });
        AssertVelocity(active, new Vector2(expectedX, 0f));
        AssertVelocity(disabled, Vector2.Zero);
    }

    [Fact]
    public void NeighborTraversalUsesRowsThenColumnsThenInsertionOrderAndCapsAtTen() {
        var world = new World();
        var system = new SteeringSystem();
        var active = SpawnAgent(world, Vector2.Zero, 10f, 0.05f);
        var orderedPositions = new[] {
            new Vector2(-1f, -1f), new Vector2(-2f, -1f), new Vector2(-3f, -1f),
            new Vector2(1f, -1f), new Vector2(2f, -1f), new Vector2(3f, -1f),
            new Vector2(-1f, 1f), new Vector2(-2f, 1f), new Vector2(-3f, 1f),
            new Vector2(1f, 1f), new Vector2(2f, 1f)
        };
        // Reverse cell insertion order, but retain insertion order inside each cell.
        foreach (var index in new[] { 9, 10, 6, 7, 8, 3, 4, 5, 0, 1, 2 }) {
            SpawnAgent(world, orderedPositions[index], 0f, 1f);
        }

        system.Update(world, DeltaTime);

        var expected = Vector2.Zero;
        foreach (var position in orderedPositions.Take(10)) {
            var distance = position.Length();
            expected -= position / distance * ((10f - distance) / 10f) * 0.05f;
        }
        AssertVelocity(active, expected);
        AssertGrid(system, "_steeringGrid", 10f, world.Entities.ToArray());
    }

    [Fact]
    public void DisabledLargeRadiusStillDeterminesCellSizeAndOutOfRangeNeighborsConsumeTheCap() {
        var world = new World();
        var system = new SteeringSystem();
        var active = SpawnAgent(world, Vector2.Zero, 2f, 1f);
        SpawnAgent(world, new Vector2(500f, 500f), 100f, 0f);
        for (var i = 0; i < 9; i++) {
            SpawnAgent(world, new Vector2(3f + i * 0.1f, 1f), 0f, 1f);
        }
        SpawnAgent(world, new Vector2(0f, 1f), 0f, 1f);
        SpawnAgent(world, new Vector2(1f, 0f), 0f, 1f);

        system.Update(world, DeltaTime);

        AssertGrid(system, "_steeringGrid", 100f, world.Entities.ToArray());
        AssertVelocity(active, new Vector2(0f, -0.5f));
    }

    [Fact]
    public void CoincidentNeighborsConsumeTheCapWithoutProducingAnAvoidanceVector() {
        var world = new World();
        var system = new SteeringSystem();
        var active = SpawnAgent(world, Vector2.Zero, 2f, 1f);
        for (var i = 0; i < 10; i++) {
            SpawnAgent(world, Vector2.Zero, 0f, 1f);
        }
        SpawnAgent(world, new Vector2(0.5f, 0f), 0f, 1f);

        system.Update(world, DeltaTime);

        AssertVelocity(active, Vector2.Zero);
    }

    [Fact]
    public void LongTravelRetainsOnlyCurrentlyOccupiedCellsAndBoundedClearedPools() {
        var world = new World();
        var system = new SteeringSystem();
        var agents = new[] {
            SpawnAgent(world, Vector2.Zero, 2f, 1f),
            SpawnAgent(world, Vector2.Zero, 2f, 0f),
            SpawnAgent(world, Vector2.Zero, 2f, 0f)
        };
        var players = new[] { SpawnPlayer(world, Vector2.Zero), SpawnPlayer(world, Vector2.Zero) };

        for (var tick = 0; tick < 160; tick++) {
            var origin = new Vector2(tick * 80f - 6400f, -tick * 80f);
            // Alternate split and merged cells to exercise surplus bucket recycling.
            for (var i = 0; i < agents.Length; i++) {
                agents[i].Position = Position(origin + new Vector2(tick % 2 == 0 ? i * 4f : 0f, 0f));
            }
            for (var i = 0; i < players.Length; i++) {
                players[i].Position = Position(origin + new Vector2(tick % 2 == 0 ? i * 40f : 0f, 1f));
            }

            system.Update(world, DeltaTime);

            AssertGrid(system, "_steeringGrid", 2f, agents);
            AssertGrid(system, "_playerGrid", 36f, players);
            AssertPool(system, "_steeringBuckets", agents.Length - Grid(system, "_steeringGrid").Count);
            AssertPool(system, "_playerBuckets", players.Length - Grid(system, "_playerGrid").Count);
        }
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(-2f, 1f)]
    [InlineData(2f, 0f)]
    public void DisablingAvoidanceClearsPreviouslyBuiltBucketsAndCanBeReenabled(float radius, float weight) {
        var world = new World();
        var system = new SteeringSystem();
        var agent = SpawnAgent(world, Vector2.Zero, 2f, 1f);
        var neighbor = SpawnAgent(world, new Vector2(0.5f, 0f), 0f, 1f);
        system.Update(world, DeltaTime);
        var previousBuckets = Buckets(system, "_steeringGrid");
        var steering = Component<SteeringComponent>(agent);

        steering.AvoidRadius = radius;
        steering.AvoidWeight = weight;
        system.Update(world, DeltaTime);

        AssertGrid(system, "_steeringGrid", 2f, Array.Empty<Entity>());
        AssertPool(system, "_steeringBuckets", 0);
        foreach (var bucket in previousBuckets) {
            AssertListEntities(bucket, Array.Empty<Entity>());
        }
        AssertVelocity(agent, Vector2.Zero);

        steering.AvoidRadius = 2f;
        steering.AvoidWeight = -1f;
        system.Update(world, DeltaTime);

        AssertGrid(system, "_steeringGrid", 2f, new[] { agent, neighbor });
        AssertVelocity(agent, new Vector2(0.75f, 0f));
    }

    [Fact]
    public void DespawningEntitiesRemovesReferencesFromListsGridsAndRecycledStorage() {
        var world = new World();
        var system = new SteeringSystem();
        var survivor = SpawnAgent(world, Vector2.Zero, 2f, 1f);
        var removedAgent = SpawnAgent(world, new Vector2(8f, 0f), 2f, 1f);
        var player = SpawnPlayer(world, new Vector2(1f, 0f));
        var removedPlayer = SpawnPlayer(world, new Vector2(80f, 0f));
        system.Update(world, DeltaTime);
        var oldBuckets = Buckets(system, "_steeringGrid").Concat(Buckets(system, "_playerGrid")).ToArray();

        Assert.True(world.Despawn(removedAgent.Id));
        Assert.True(world.Despawn(removedPlayer.Id));
        system.Update(world, DeltaTime);

        AssertListEntities(Field(system, "_steeredAgents"), new[] { survivor });
        AssertListEntities(Field(system, "_playerTargets"), new[] { player });
        AssertGrid(system, "_steeringGrid", 2f, new[] { survivor });
        AssertGrid(system, "_playerGrid", 36f, new[] { player });
        AssertPool(system, "_steeringBuckets", 0);
        AssertPool(system, "_playerBuckets", 0);
        foreach (var bucket in oldBuckets) {
            AssertNoRetainedEntity(bucket, removedAgent, removedPlayer);
        }

        Assert.True(world.Despawn(player.Id));
        system.Update(world, DeltaTime);

        AssertListEntities(Field(system, "_playerTargets"), Array.Empty<Entity>());
        AssertGrid(system, "_playerGrid", 36f, Array.Empty<Entity>());
        AssertPool(system, "_playerBuckets", 0);
        AssertVelocity(survivor, Vector2.Zero);
        foreach (var bucket in oldBuckets) {
            AssertNoRetainedEntity(bucket, removedAgent, removedPlayer, player);
        }
    }

    [Theory]
    [InlineData("despawn")]
    [InlineData("steering")]
    [InlineData("velocity")]
    public void EmptySteeredWorldClearsAllCachedEntityReferencesEvenWhenPlayersRemain(string removal) {
        var world = new World();
        var system = new SteeringSystem();
        var agent = SpawnAgent(world, Vector2.Zero, 2f, 1f);
        SpawnPlayer(world, new Vector2(1f, 0f));
        system.Update(world, DeltaTime);
        var oldBuckets = Buckets(system, "_steeringGrid").Concat(Buckets(system, "_playerGrid")).ToArray();

        Assert.True(removal switch {
            "despawn" => world.Despawn(agent.Id),
            "steering" => agent.RemoveComponent<SteeringComponent>(),
            _ => agent.RemoveComponent<VelocityComponent>()
        });
        for (var tick = 0; tick < 2; tick++) {
            system.Update(world, DeltaTime);

            AssertListEntities(Field(system, "_steeredAgents"), Array.Empty<Entity>());
            AssertListEntities(Field(system, "_playerTargets"), Array.Empty<Entity>());
            AssertGrid(system, "_steeringGrid", 2f, Array.Empty<Entity>());
            AssertGrid(system, "_playerGrid", 36f, Array.Empty<Entity>());
            AssertPool(system, "_steeringBuckets", 0);
            AssertPool(system, "_playerBuckets", 0);
            foreach (var bucket in oldBuckets) {
                AssertListEntities(bucket, Array.Empty<Entity>());
            }
        }
    }

    public static IEnumerable<object[]> EqualDistanceTargets() {
        yield return Tie("center before next ring", new Vector2(35f, 18f),
            new Vector2(36f, 18f), new Vector2(34f, 18f), 1);
        yield return Tie("earlier ring before insertion order", new Vector2(18f, 18f),
            new Vector2(78f, 18f), new Vector2(54f, 66f), 1);
        yield return Tie("horizontal edges visit x increasing", new Vector2(18f, 18f),
            new Vector2(54f, -18f), new Vector2(-18f, 54f), 1);
        yield return Tie("bottom before top at the same x", new Vector2(18f, 18f),
            new Vector2(18f, 54f), new Vector2(18f, -18f), 1);
        yield return Tie("horizontal edges before vertical edges", new Vector2(18f, 18f),
            new Vector2(54f, 18f), new Vector2(18f, 54f), 1);
        yield return Tie("vertical edges visit y increasing", new Vector2(18f, 18f),
            new Vector2(-54f, 54f), new Vector2(90f, -18f), 1);
        yield return Tie("left before right at the same y", new Vector2(18f, 18f),
            new Vector2(90f, 18f), new Vector2(-54f, 18f), 1);
        yield return Tie("center bucket insertion order", new Vector2(18f, 18f),
            new Vector2(23f, 18f), new Vector2(13f, 18f), 0);
        yield return Tie("center bucket reversed insertion order", new Vector2(18f, 18f),
            new Vector2(13f, 18f), new Vector2(23f, 18f), 0);
        yield return Tie("sparse far bucket insertion order", new Vector2(18f, 18f),
            new Vector2(3618f, 19f), new Vector2(3618f, 17f), 0);
        yield return Tie("sparse far bucket reversed insertion order", new Vector2(18f, 18f),
            new Vector2(3618f, 17f), new Vector2(3618f, 19f), 0);
        yield return Tie("sparse far cells visit left before right", new Vector2(18f, 18f),
            new Vector2(3618f, 18f), new Vector2(-3582f, 18f), 1);
        yield return Tie("sparse distinct rings before insertion order", new Vector2(18f, 18f),
            new Vector2(618f, 18f), new Vector2(378f, 498f), 1);
        yield return Tie("sparse horizontal edges visit x increasing", new Vector2(18f, 18f),
            new Vector2(378f, -342f), new Vector2(-342f, 378f), 1);
        yield return Tie("sparse bottom before top at the same x", new Vector2(18f, 18f),
            new Vector2(18f, 378f), new Vector2(18f, -342f), 1);
        yield return Tie("sparse horizontal edges before vertical edges", new Vector2(18f, 18f),
            new Vector2(378f, 18f), new Vector2(18f, 378f), 1);
        yield return Tie("sparse vertical edges visit y increasing", new Vector2(18f, 18f),
            new Vector2(-702f, 378f), new Vector2(738f, -342f), 1);
    }

    [Theory]
    [MemberData(nameof(EqualDistanceTargets))]
    public void NearestTargetTiesPreserveOriginalCellVisitationAndBucketOrder(
        string scenario, Vector2 origin, Vector2 first, Vector2 second, int winner) {
        var world = new World();
        var system = new SteeringSystem();
        var agent = SpawnAgent(world, origin);
        Component<SteeringComponent>(agent).SeekWeight = 1f;
        SpawnPlayer(world, first);
        SpawnPlayer(world, second);
        Assert.True((first - origin).LengthSquared() == (second - origin).LengthSquared(), scenario);

        for (var tick = 0; tick < 3; tick++) {
            system.Update(world, DeltaTime);
        }

        AssertVelocity(agent, Vector2.Normalize((winner == 0 ? first : second) - origin));
    }

    [Fact]
    public void SparseFarTargetsChooseTheNearestRatherThanFirstOrNearestCell() {
        var world = new World();
        var system = new SteeringSystem();
        var agent = SpawnAgent(world, new Vector2(18f, 18f));
        Component<SteeringComponent>(agent).SeekWeight = 1f;
        SpawnPlayer(world, new Vector2(-7182f, 18f));
        SpawnPlayer(world, new Vector2(18f, 10818f));
        // Ring 99 is visited before ring 100, but its diagonal target is farther away.
        SpawnPlayer(world, new Vector2(3582f, 3582f));
        var nearest = SpawnPlayer(world, new Vector2(3618f, 18f));

        for (var tick = 0; tick < 3; tick++) {
            system.Update(world, DeltaTime);
        }
        AssertVelocity(agent, Vector2.UnitX);

        nearest.Position = new Vector3(18f, 0f, -3582f);
        for (var tick = 0; tick < 3; tick++) {
            system.Update(world, DeltaTime);
        }
        AssertVelocity(agent, -Vector2.UnitY);
        AssertGrid(system, "_playerGrid", 36f, world.Query<InputComponent>().Select(pair => pair.Entity).ToArray());
    }

    [Theory]
    [InlineData(17.99f, 1)]
    [InlineData(18f, 1)]
    [InlineData(18.01f, 2)]
    [InlineData(35.99f, 2)]
    [InlineData(36f, 2)]
    [InlineData(36.01f, 3)]
    [InlineData(200f, 3)]
    public void CadenceUsesDistanceBoundariesAndTickPlusEntityIdWhileMovementContinues(float distance, int interval) {
        for (var id = 1; id <= 3; id++) {
            var world = new World();
            var system = new SteeringSystem();
            var movement = new MovementSystem();
            for (var padding = 1; padding < id; padding++) {
                world.Spawn();
            }
            var agent = SpawnAgent(world, Vector2.Zero);
            Component<SteeringComponent>(agent).SeekWeight = 1f;
            var player = SpawnPlayer(world, new Vector2(distance, 0f));
            Assert.Equal(id, agent.Id);

            for (var tick = 1; tick <= 6; tick++) {
                player.Position = agent.Position + new Vector3(distance, 0f, 0f);
                var velocity = Component<VelocityComponent>(agent);
                velocity.X = -0.25f;
                velocity.Z = 0.75f;
                var expected = (tick + agent.Id) % interval == 0
                    ? Vector2.UnitX : new Vector2(-0.25f, 0.75f);

                system.Update(world, DeltaTime);
                AssertVelocity(agent, expected);
                var before = agent.Position;
                movement.Update(world, 0.25f);
                var expectedPosition = before + Position(expected) * 0.25f;
                Assert.Equal(expectedPosition.X, agent.Position.X, 5);
                Assert.Equal(expectedPosition.Y, agent.Position.Y, 5);
                Assert.Equal(expectedPosition.Z, agent.Position.Z, 5);
            }
        }
    }

    [Fact]
    public void PlayerArrivalDepartureAndReturnSwitchBetweenSeekAndNoPlayerMovement() {
        var world = new World();
        var system = new SteeringSystem();
        var movement = new MovementSystem();
        var agent = SpawnAgent(world, Vector2.Zero);
        var steering = Component<SteeringComponent>(agent);
        steering.SeekWeight = 1f;
        steering.WanderWeight = 0.25f;
        steering.WanderJitter = 0f;
        steering.WanderAngle = 0f;

        system.Update(world, DeltaTime);
        AssertVelocity(agent, new Vector2(0.25f, 0f));
        movement.Update(world, 1f);
        Assert.Equal(new Vector3(0.25f, 0f, 0f), agent.Position);

        var player = SpawnPlayer(world, new Vector2(-5f, 0f));
        system.Update(world, DeltaTime);
        AssertVelocity(agent, new Vector2(-0.75f, 0f));
        movement.Update(world, 1f);
        Assert.Equal(new Vector3(-0.5f, 0f, 0f), agent.Position);

        Assert.True(world.Despawn(player.Id));
        system.Update(world, DeltaTime);
        AssertVelocity(agent, new Vector2(0.25f, 0f));
        AssertGrid(system, "_playerGrid", 36f, Array.Empty<Entity>());
        movement.Update(world, 1f);
        Assert.Equal(new Vector3(-0.25f, 0f, 0f), agent.Position);

        SpawnPlayer(world, new Vector2(5f, 0f));
        system.Update(world, DeltaTime);
        AssertVelocity(agent, Vector2.UnitX);
        movement.Update(world, 1f);
        Assert.Equal(new Vector3(0.75f, 0f, 0f), agent.Position);
    }

    [Fact]
    public void NoPlayerSteeringRunsEveryTickAndFarPlayerReturnResumesGlobalCadence() {
        var world = new World();
        var system = new SteeringSystem();
        var agent = SpawnAgent(world, Vector2.Zero);
        var steering = Component<SteeringComponent>(agent);
        steering.SeekWeight = 1f;
        steering.WanderWeight = 0.25f;
        steering.WanderJitter = 0f;
        Assert.Equal(1, agent.Id);

        system.Update(world, DeltaTime); // Tick 1: no players, no cadence restriction.
        AssertVelocity(agent, new Vector2(0.25f, 0f));
        var player = SpawnPlayer(world, new Vector2(-100f, 0f));
        system.Update(world, DeltaTime); // Tick 2: (2 + 1) % 3 == 0.
        AssertVelocity(agent, new Vector2(-0.75f, 0f));

        Assert.True(world.Despawn(player.Id));
        system.Update(world, DeltaTime); // Tick 3 must not retain the old seek velocity.
        AssertVelocity(agent, new Vector2(0.25f, 0f));
        player = SpawnPlayer(world, new Vector2(-100f, 0f));
        system.Update(world, DeltaTime); // Tick 4: returning players do not reset the tick.
        AssertVelocity(agent, new Vector2(0.25f, 0f));
        system.Update(world, DeltaTime); // Tick 5 is the next scheduled far update.
        AssertVelocity(agent, new Vector2(-0.75f, 0f));

        Assert.True(world.Despawn(player.Id));
        system.Update(world, DeltaTime);
        AssertVelocity(agent, new Vector2(0.25f, 0f));
        steering.WanderWeight = 0f;
        system.Update(world, DeltaTime);
        AssertVelocity(agent, Vector2.Zero);
    }

    private static object[] Tie(string name, Vector2 origin, Vector2 first, Vector2 second, int winner) =>
        new object[] { name, origin, first, second, winner };

    private static Entity SpawnAgent(World world, Vector2 position, float radius = 0f, float weight = 0f) {
        var entity = world.Spawn(Position(position));
        entity.AddComponent(new VelocityComponent());
        entity.AddComponent(new SteeringComponent {
            MoveSpeed = 1f, AvoidRadius = radius, AvoidWeight = weight,
            SeekWeight = 0f, CircleWeight = 0f, WanderWeight = 0f, RoamRadius = 0f
        });
        return entity;
    }

    private static Entity SpawnPlayer(World world, Vector2 position) {
        var entity = world.Spawn(Position(position));
        entity.AddComponent(new InputComponent());
        return entity;
    }

    private static Vector3 Position(Vector2 position) => new(position.X, 0f, position.Y);

    private static T Component<T>(Entity entity) where T : class, IComponent {
        Assert.True(entity.TryGetComponent<T>(out var component));
        return component;
    }

    private static void AssertVelocity(Entity entity, Vector2 expected) {
        var velocity = Component<VelocityComponent>(entity);
        Assert.Equal(expected.X, velocity.X, 5);
        Assert.Equal(expected.Y, velocity.Z, 5);
    }

    private static object Field(object instance, string name) {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null) {
            return field.GetValue(instance)!;
        }

        // Steering grids/lists now live on SteeringSystem's shared SteeringContext.
        var contextField = instance.GetType().GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(contextField);
        var context = contextField.GetValue(instance)!;
        var nestedField = context.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(nestedField);
        return nestedField.GetValue(context)!;
    }

    private static IDictionary Grid(SteeringSystem system, string name) =>
        Assert.IsAssignableFrom<IDictionary>(Field(system, name));

    private static object[] Buckets(SteeringSystem system, string name) =>
        Grid(system, name).Values.Cast<object>().ToArray();

    private static long CellKey(Entity entity, float cellSize) {
        var x = (int)MathF.Floor(entity.Position.X / cellSize);
        var z = (int)MathF.Floor(entity.Position.Z / cellSize);
        return ((long)x << 32) | (uint)z;
    }

    private static void AssertGrid(SteeringSystem system, string name, float cellSize, Entity[] entities) {
        var grid = Grid(system, name);
        var expected = entities.GroupBy(entity => CellKey(entity, cellSize)).ToArray();
        Assert.Equal(expected.Length, grid.Count);
        foreach (var group in expected) {
            Assert.True(grid.Contains(group.Key), $"Missing occupied cell {group.Key} in {name}");
            AssertListEntities(grid[group.Key]!, group.ToArray());
        }
    }

    private static void AssertPool(SteeringSystem system, string name, int maximumBuckets) {
        var pool = Assert.IsAssignableFrom<IEnumerable>(Field(system, name)).Cast<object>().ToArray();
        Assert.InRange(pool.Length, 0, maximumBuckets);
        foreach (var bucket in pool) {
            AssertListEntities(bucket, Array.Empty<Entity>());
        }
    }

    private static Entity? EntryEntity(object entry) {
        var property = entry.GetType().GetProperty("Entity");
        Assert.NotNull(property);
        return (Entity?)property.GetValue(entry);
    }

    private static void AssertListEntities(object value, Entity[] expected) {
        var list = Assert.IsAssignableFrom<IList>(value);
        Assert.Equal(expected.Length, list.Count);
        for (var i = 0; i < expected.Length; i++) {
            Assert.Same(expected[i], EntryEntity(list[i]!));
        }
        // Count alone cannot detect references left in the reusable backing array.
        var items = Assert.IsAssignableFrom<Array>(Field(value, "_items"));
        for (var i = list.Count; i < items.Length; i++) {
            Assert.Null(EntryEntity(items.GetValue(i)!));
        }
    }

    private static void AssertNoRetainedEntity(object list, params Entity[] removed) {
        var items = Assert.IsAssignableFrom<Array>(Field(list, "_items"));
        foreach (var item in items) {
            Assert.DoesNotContain(EntryEntity(item!), removed);
        }
    }
}
