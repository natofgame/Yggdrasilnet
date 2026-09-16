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
using Yggdrasilnet.Shared.Spell;
using SharedAction = Yggdrasilnet.Shared.Network.Packet.Snapshot.Components.ActionComponent;
using SharedHealth = Yggdrasilnet.Shared.Network.Packet.Snapshot.Components.HealthComponent;
using SharedProjectile = Yggdrasilnet.Shared.Network.Packet.Snapshot.Components.ProjectileComponent;

namespace Yggdrasilnet.Server.Tests;

public sealed class CombatSnapshotTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HealthCurrentAndMaxChangesEachBypassMovementCadence(bool changeMax, bool stationary) {
        var (builder, session, target) = NewScene(stationary);
        var health = new HealthComponent { Current = 75.5f, Max = 100f };
        target.AddComponent(health);
        var initial = Find(builder.BuildSnapshotForSession(session, 0, 60, true), target.Id);

        if (changeMax) {
            health.Max = 125.25f;
        } else {
            health.Current = 50.25f;
        }

        var changed = Find(builder.BuildSnapshotForSession(session, 1, 60, false), target.Id);
        AssertHealth(health, Component<SharedHealth>(changed));
        Assert.Equal(75.5f, Component<SharedHealth>(initial).Current);
        Assert.Equal(100f, Component<SharedHealth>(initial).Max);
        AssertNoTarget(builder.BuildSnapshotForSession(session, 2, 60, false), target.Id);
        AssertNoTarget(builder.BuildSnapshotForSession(session, 61, 60, false), target.Id);
    }

    [Theory]
    [InlineData("ActionType", false)]
    [InlineData("ActionType", true)]
    [InlineData("Phase", false)]
    [InlineData("Phase", true)]
    [InlineData("PhaseProgress01", false)]
    [InlineData("PhaseProgress01", true)]
    [InlineData("SpellId", false)]
    [InlineData("SpellId", true)]
    [InlineData("TargetEntityId", false)]
    [InlineData("TargetEntityId", true)]
    [InlineData("SpellType", false)]
    [InlineData("SpellType", true)]
    [InlineData("Idle", false)]
    [InlineData("Idle", true)]
    public void EachActionFieldChangeBypassesMovementCadence(string field, bool stationary) {
        var (builder, session, target) = NewScene(stationary);
        var action = NewAction("fire");
        target.AddComponent(action);
        var initial = Find(builder.BuildSnapshotForSession(session, 0, 60, true), target.Id);
        var initialBytes = Serialize(initial);

        switch (field) {
            case "ActionType": action.ActionType = 2; break;
            case "Phase": action.Phase = SpellPhase.Strike; break;
            case "PhaseProgress01": action.PhaseProgress01 = 0.2501f; break;
            case "SpellId": action.SpellId = "氷❄"; break;
            case "TargetEntityId": action.TargetEntityId++; break;
            case "SpellType": action.SpellType = SpellType.Targeted; break;
            case "Idle": action.Phase = SpellPhase.None; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }

        var changed = Find(builder.BuildSnapshotForSession(session, 1, 60, false), target.Id);
        AssertAction(action, Component<SharedAction>(changed));
        Assert.Equal(initialBytes, Serialize(initial));
        Assert.True(target.HasComponent<ActionStateComponent>());
        AssertNoTarget(builder.BuildSnapshotForSession(session, 2, 60, false), target.Id);
        AssertNoTarget(builder.BuildSnapshotForSession(session, 61, 60, false), target.Id);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void UnchangedEntitiesDoNotProduceDeltasEvenWhenCadenceIsDue(bool combat, bool stationary) {
        var (builder, session, target) = NewScene(stationary);
        if (combat) {
            target.AddComponent(new HealthComponent { Current = 80, Max = 100 });
            target.AddComponent(NewAction("fire"));
        }
        Find(builder.BuildSnapshotForSession(session, 0, 60, true), target.Id);

        foreach (var tick in new long[] { 1, 30, 60, 61, 120 }) {
            var packet = builder.BuildSnapshotForSession(session, tick, 60, false);
            Assert.Equal(session.EntityId, Assert.Single(packet.Entities).EntityId);
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void CombatComponentPresenceChangesBypassStationaryThrottle(bool health, bool adding) {
        var (builder, session, target) = NewScene(true);
        IComponent component = health ? new HealthComponent() : new ActionStateComponent();
        if (!adding) {
            target.AddComponent(component);
        }
        Find(builder.BuildSnapshotForSession(session, 0, 60, true), target.Id);

        if (adding) {
            target.AddComponent(component);
        } else if (health) {
            Assert.True(target.RemoveComponent<HealthComponent>());
        } else {
            Assert.True(target.RemoveComponent<ActionStateComponent>());
        }

        var changed = Find(builder.BuildSnapshotForSession(session, 1, 60, false), target.Id);
        var type = health ? NetworkedComponentType.Health : NetworkedComponentType.Action;
        Assert.Equal(adding, changed.Components.Any(value => value.Type == type));
        if (adding && !health) {
            Assert.Equal(SpellPhase.None, Component<SharedAction>(changed).Phase);
        }
        AssertNoTarget(builder.BuildSnapshotForSession(session, 2, 60, false), target.Id);
        AssertNoTarget(builder.BuildSnapshotForSession(session, 61, 60, false), target.Id);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData("fireball", false)]
    [InlineData("fireball", true)]
    [InlineData("氷の矢-é-🔥", false)]
    [InlineData("氷の矢-é-🔥", true)]
    public void FullKeyframesRoundTripIndependentSharedCombatCopies(string spellId, bool broadcast) {
        var world = new World();
        var owner = world.Spawn(new Vector3(1.25f, -2.5f, 3.75f));
        var health = new HealthComponent { Current = 63.25f, Max = 125.5f };
        var projectile = new ProjectileComponent {
            SpellId = spellId, CasterEntityId = 17, TargetEntityId = 29,
            Speed = 9, ElapsedSeconds = 0.5f
        };
        var action = NewAction(spellId);
        owner.AddComponent(health);
        owner.AddComponent(projectile);
        owner.AddComponent(action);
        var builder = new SnapshotBuilder(world);
        using var batch = broadcast ? BeginBatch(builder, 0) : null;

        EntitySnapshot Build() => Assert.Single((batch is null
            ? builder.BuildSnapshotForSession(NewSession(owner.Id), 0, 60, true)
            : batch.Build(NewSession(owner.Id), true)).Entities);

        var snapshot = Build();
        Assert.Equal(3, snapshot.Components.Count);
        var copiedHealth = Component<SharedHealth>(snapshot);
        var copiedProjectile = Component<SharedProjectile>(snapshot);
        var copiedAction = Component<SharedAction>(snapshot);
        Assert.NotSame(health, copiedHealth);
        Assert.NotSame(projectile, copiedProjectile);
        Assert.NotSame(action, copiedAction);
        AssertHealth(health, copiedHealth);
        AssertProjectile(projectile, copiedProjectile);
        AssertAction(action, copiedAction);
        var bytes = Serialize(snapshot);
        Assert.Equal(bytes.Length, snapshot.EstimatedBytes);

        var reader = new NetDataReader(bytes);
        var decoded = EntitySnapshot.ReadFrom(reader, NetworkedComponentRegistry.Default);
        Assert.Equal(0, reader.AvailableBytes);
        Assert.Equal(owner.Id, decoded.EntityId);
        Assert.Equal(owner.Position, new Vector3(decoded.PositionX, decoded.PositionY, decoded.PositionZ));
        Assert.Equal(snapshot.DefinitionId, decoded.DefinitionId);
        Assert.Equal(snapshot.LastInputSequence, decoded.LastInputSequence);
        Assert.Equal(3, decoded.Components.Count);
        AssertHealth(health, Component<SharedHealth>(decoded));
        AssertProjectile(projectile, Component<SharedProjectile>(decoded));
        AssertAction(action, Component<SharedAction>(decoded));
        Assert.Equal(bytes, Serialize(decoded));

        var second = Build();
        if (!broadcast) {
            Assert.NotSame(snapshot, second);
            Assert.NotSame(copiedHealth, Component<SharedHealth>(second));
            Assert.NotSame(copiedProjectile, Component<SharedProjectile>(second));
            Assert.NotSame(copiedAction, Component<SharedAction>(second));
        }

        health.Current = 1;
        health.Max = 2;
        projectile.SpellId = "changed";
        projectile.CasterEntityId = 90;
        projectile.TargetEntityId = 91;
        action.ActionType = 3;
        action.Phase = SpellPhase.None;
        action.PhaseProgress01 = 0;
        action.SpellId = "changed";
        action.TargetEntityId = 92;
        action.SpellType = SpellType.Self;
        Assert.Equal(bytes, Serialize(snapshot));
        Assert.Equal(bytes, Serialize(second));

        copiedHealth.Current = -1;
        copiedProjectile.CasterEntityId = -1;
        copiedAction.TargetEntityId = -1;
        Assert.Equal(1f, health.Current);
        Assert.Equal(90, projectile.CasterEntityId);
        Assert.Equal(92, action.TargetEntityId);
        if (!broadcast) {
            Assert.Equal(bytes, Serialize(second));
        }
    }

    [Theory]
    [InlineData(NetworkedComponentType.Health, "")]
    [InlineData(NetworkedComponentType.Projectile, "")]
    [InlineData(NetworkedComponentType.Projectile, "氷-é-🔥")]
    [InlineData(NetworkedComponentType.Action, "")]
    [InlineData(NetworkedComponentType.Action, "氷-é-🔥")]
    [InlineData(NetworkedComponentType.Action, "fireball")]
    public void IndividualCombatComponentByteEstimatesMatchWireLength(NetworkedComponentType type, string spellId) {
        var world = new World();
        var owner = world.Spawn();
        IComponent component = type switch {
            NetworkedComponentType.Health => new HealthComponent { Current = 10, Max = 20 },
            NetworkedComponentType.Projectile => new ProjectileComponent {
                SpellId = spellId, CasterEntityId = 1, TargetEntityId = 2
            },
            NetworkedComponentType.Action => NewAction(spellId),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        owner.AddComponent(component);
        var snapshot = Assert.Single(new SnapshotBuilder(world)
            .BuildSnapshotForSession(NewSession(owner.Id), 0, 60, true).Entities);
        Assert.Equal(type, Assert.Single(snapshot.Components).Type);

        var writer = new NetDataWriter();
        snapshot.WriteTo(writer);

        Assert.Equal(writer.Length, snapshot.EstimatedBytes);
    }

    private static (SnapshotBuilder Builder, PlayerSession Session, Entity Target) NewScene(bool stationary) {
        var world = new World();
        var owner = world.Spawn();
        // Near stationary and far moving entities exercise two independent cadence restrictions.
        var target = world.Spawn(new Vector3(stationary ? 5 : 35, 0, 0));
        target.AddComponent(new VelocityComponent { X = stationary ? 0 : 1 });
        return (new SnapshotBuilder(world), NewSession(owner.Id), target);
    }

    private static ActionStateComponent NewAction(string spellId) => new() {
        ActionType = 1,
        Phase = SpellPhase.Anticipation,
        PhaseProgress01 = 0.25f,
        SpellId = spellId,
        TargetEntityId = 29,
        SpellType = SpellType.Projectile,
        PhaseTimeRemaining = 0.75f,
        CurrentPhaseDuration = 1,
        CasterEntityId = 17
    };

    private static PlayerSession NewSession(int entityId) =>
        new((NetPeer)RuntimeHelpers.GetUninitializedObject(typeof(NetPeer)), 0) { EntityId = entityId };

    private static EntitySnapshot Find(SnapshotPacket packet, int entityId) =>
        Assert.Single(packet.Entities.Where(entity => entity.EntityId == entityId));

    private static void AssertNoTarget(SnapshotPacket packet, int entityId) =>
        Assert.DoesNotContain(packet.Entities, entity => entity.EntityId == entityId);

    private static T Component<T>(EntitySnapshot snapshot) where T : INetworkedComponent =>
        Assert.IsType<T>(Assert.Single(snapshot.Components.OfType<T>()));

    private static byte[] Serialize(EntitySnapshot snapshot) {
        var writer = new NetDataWriter();
        snapshot.WriteTo(writer);
        return writer.CopyData();
    }

    private static void AssertHealth(SharedHealth expected, SharedHealth actual) {
        Assert.Equal(expected.Current, actual.Current);
        Assert.Equal(expected.Max, actual.Max);
    }

    private static void AssertProjectile(SharedProjectile expected, SharedProjectile actual) {
        Assert.Equal(expected.SpellId, actual.SpellId);
        Assert.Equal(expected.CasterEntityId, actual.CasterEntityId);
        Assert.Equal(expected.TargetEntityId, actual.TargetEntityId);
    }

    private static void AssertAction(SharedAction expected, SharedAction actual) {
        Assert.Equal(expected.ActionType, actual.ActionType);
        Assert.Equal(expected.Phase, actual.Phase);
        Assert.Equal(expected.PhaseProgress01, actual.PhaseProgress01);
        Assert.Equal(expected.SpellId, actual.SpellId);
        Assert.Equal(expected.TargetEntityId, actual.TargetEntityId);
        Assert.Equal(expected.SpellType, actual.SpellType);
    }

    private static ReflectedBatch BeginBatch(SnapshotBuilder builder, long tick) {
        var method = typeof(SnapshotBuilder).GetMethod("BeginBroadcastBatch",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var scope = method.Invoke(builder, new object[] { tick });
        Assert.NotNull(scope);
        return new ReflectedBatch(scope);
    }

    private sealed class ReflectedBatch(object scope) : IDisposable {
        public SnapshotPacket Build(PlayerSession session, bool force) {
            var method = scope.GetType().GetMethod("Build", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method);
            return Assert.IsType<SnapshotPacket>(method.Invoke(scope, new object[] { session, 60, force }));
        }

        public void Dispose() => Assert.IsAssignableFrom<IDisposable>(scope).Dispose();
    }
}
