using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using LiteNetLib;
using Serilog;
using Yggdrasilnet.Server.Handlers;
using Yggdrasilnet.Server.Simulation.Content;
using ContentEntity = Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.Session;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Utils;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet;
using Yggdrasilnet.Shared.Network.Packet.Packets;
using Yggdrasilnet.Shared.Network.Packet.Snapshot;

namespace Yggdrasilnet.Server.Simulation;

public sealed class Simulation : ISimulationContext {
    private readonly record struct SnapshotDistanceTier(float MaxDistance, int TargetHz);
    private readonly record struct SnapshotDistanceTierSquared(float MaxDistanceSquared, int TargetHz);

    public ConcurrentQueue<ISimulationEvent> IncomingEvents { get; } = new();

    private readonly PacketDispatcher<ISimulationContext> _dispatcher = new();
    private readonly PlayerSessionRegistry _sessions = new();
    private readonly World.World _world;

    private readonly DefinitionRegistry<ContentEntity.EntityDefinition> _entityDefinitions = new();
    private readonly EntityFactory _entityFactory = new();
    private readonly Dictionary<(int X, int Y), List<World.Entity>> _interestGrid = new();
    private long _interestGridBuiltAtTick = -1;

    public NetServer? NetServer { get; set; }
    
    public long Tick { get; private set; }
    public PlayerSessionRegistry Sessions => _sessions;
    public World.World World => _world;

    public Simulation() {
        _dispatcher.Register(PacketType.PlayerConnexion, new PlayerConnexionHandler());
        _dispatcher.Register(PacketType.Input, new InputHandler());
        _dispatcher.Register(PacketType.SpawnEntities, new SpawnEntitiesHandler());
        
        _world = new World.World();
        _world.Load();

        _entityDefinitions.Load(ContentPaths.Resolve("Entities"));

        SpawnEntities("crowd", ReadCrowdSizeFromEnv());
    }

    private const string CrowdSizeEnvVar = "YGG_CROWD_SIZE";
    private const int DefaultCrowdSize = 0;
    private const float CrowdAreaSize = 30f;
    private const int MaxEntitiesPerSpawnRequest = 20_000;
    private const float SnapshotGridCellSize = 20f;
    private const int MaxStationaryEntityHz = 2;
    private const float StationarySpeedSquaredEpsilon = 0.0001f;
    private const float PositionDeltaThreshold = 0.10f;
    private const float VelocityDeltaThreshold = 0.05f;

    private static readonly SnapshotDistanceTier[] SnapshotDistanceTiers = [
        new(15f, 20),
        new(35f, 8),
        new(60f, 2),
    ];

    private static readonly SnapshotDistanceTierSquared[] SnapshotDistanceTiersSquared =
        SnapshotDistanceTiers.Select(tier => new SnapshotDistanceTierSquared(tier.MaxDistance * tier.MaxDistance, tier.TargetHz)).ToArray();
    
    public void SpawnEntities(string definitionId, int count) {
        count = Math.Clamp(count, 0, MaxEntitiesPerSpawnRequest);
        if (count <= 0) {
            return;
        }

        if (!_entityDefinitions.TryGet(definitionId, out var definition)) {
            Log.Warning("Entity definition '{DefinitionId}' not found, cannot spawn {Count} entities", definitionId, count);
            return;
        }

        var half = CrowdAreaSize / 2f;
        var random = new Random();
        for (var i = 0; i < count; i++) {
            var position = new Vector3(
                (float)(random.NextDouble() * 2f - 1f) * half,
                0f,
                (float)(random.NextDouble() * 2f - 1f) * half);
            _entityFactory.Create(definition, World, position);
        }

        Log.Information("Spawned {Count} '{DefinitionId}' entities ({Total} total entities now)",
            count, definitionId, World.Entities.Count);
    }

    private static int ReadCrowdSizeFromEnv() {
        var raw = Environment.GetEnvironmentVariable(CrowdSizeEnvVar);
        return int.TryParse(raw, out var count) ? count : DefaultCrowdSize;
    }

    public void Update(float deltaTime) {
        Tick++;

        DrainIncomingEvents();
        _world.Update(deltaTime);
    }

    private void DrainIncomingEvents() {
        while (IncomingEvents.TryDequeue(out var worldEvent)) {
            switch (worldEvent) {
                case PeerConnectedEvent peerConnected:
                    CreatePlayer(peerConnected.Peer);
                    break;
                case PeerDisconnectedEvent peerDisconnected:
                    RemovePlayer(peerDisconnected.Peer, peerDisconnected.Info);
                    break;
                case PacketReceivedEvent packetReceived:
                    if (!_dispatcher.Dispatch(packetReceived.Peer, packetReceived.Packet, this)) {
                        Log.Warning("No handler registered for packet {PacketType} from {EndPoint}",
                            packetReceived.Packet.PacketType, packetReceived.Peer.Address);
                    }
                    break;
            }
        }
    }

    private void CreatePlayer(NetPeer peer) {
        var session = _sessions.Create(peer, Tick);

        if (!_entityDefinitions.TryGet("player", out var playerDefinition)) {
            Log.Warning("Entity definition 'player' not found, disconnecting {EndPoint}", peer.Address);
            peer.Disconnect();
            return;
        }

        var entity = _entityFactory.Create(playerDefinition, World, Vector3.Zero);
        session.EntityId = entity.Id;
        
        NetServer?.Send(peer, new PlayerConnexionPacket { EntityId = entity.Id, IsOwner = true });
        Log.Information("Player {PlayerId} connected: {EndPoint} entity: {EntityId}", session.Id, peer.Address, entity.Id);
    }

    private void RemovePlayer(NetPeer peer, DisconnectInfo info) {
        if (!_sessions.Remove(peer, out var session)) {
            return;
        }

        if (session != null) {
            var id = session.EntityId;
            if (id == -1) {
                return;
            }
            World.Despawn(id);
        }

        Log.Information("Player {PlayerId} disconnected: {EndPoint} ({Reason})", session!.Id, peer.Address, info.Reason);
    }

    public SnapshotPacket BuildSnapshotForSession(PlayerSession session, int tickRate, bool forceFullSnapshot) {
        var packet = new SnapshotPacket();
        if (session.EntityId == -1) {
            return packet;
        }

        if (!World.TryGetEntity(session.EntityId, out var ownerEntity)) {
            return packet;
        }

        var observerPosition = Flat(ownerEntity.Position);
        packet.Entities.Add(BuildEntitySnapshot(ownerEntity));
        var sentStates = session.LastSentEntities;
        UpsertSentState(sentStates, ownerEntity, Tick, forceFullSnapshot);

        EnsureInterestGrid();
        var maxDistance = SnapshotDistanceTiers[^1].MaxDistance;
        foreach (var entity in EnumerateNearbyEntities(observerPosition, maxDistance)) {
            if (entity.Id == ownerEntity.Id) {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(observerPosition, Flat(entity.Position));
            var targetHz = ResolveTargetHz(distanceSquared);
            if (targetHz <= 0) {
                sentStates.Remove(entity.Id);
                continue;
            }

            if (IsStationary(entity)) {
                targetHz = Math.Min(targetHz, MaxStationaryEntityHz);
            }

            sentStates.TryGetValue(entity.Id, out var state);
            if (!forceFullSnapshot && !IsEntityDueForSend(state, targetHz, tickRate, Tick)) {
                continue;
            }

            if (!forceFullSnapshot && state != null && !HasSignificantChange(entity, state)) {
                continue;
            }

            packet.Entities.Add(BuildEntitySnapshot(entity));
            UpsertSentState(sentStates, entity, Tick, forceFullSnapshot);
        }

        return packet;
    }

    private static EntitySnapshot BuildEntitySnapshot(World.Entity entity) {
        var snapshot = new EntitySnapshot {
            EntityId = entity.Id,
            PositionX = entity.Position.X,
            PositionY = entity.Position.Y,
            PositionZ = entity.Position.Z
        };

        foreach (var component in entity.Components) {
            if (component is INetworkedComponent networked) {
                snapshot.Components.Add(networked);
            }
        }

        if (entity.TryGetComponent<InputComponent>(out var input)) {
            snapshot.LastInputSequence = input.LastSequence;
        }

        return snapshot;
    }

    private int ResolveTargetHz(float distanceSquared) {
        foreach (var tier in SnapshotDistanceTiersSquared) {
            if (distanceSquared <= tier.MaxDistanceSquared) {
                return tier.TargetHz;
            }
        }

        return 0;
    }

    private static bool IsEntityDueForSend(SentEntityState? state, int targetHz, int tickRate, long currentTick) {
        if (state == null) {
            return true;
        }

        var safeTickRate = Math.Max(1, tickRate);
        var safeHz = Math.Max(1, targetHz);
        var intervalTicks = Math.Max(1, (int)MathF.Round(safeTickRate / (float)safeHz));
        return currentTick - state.LastSentTick >= intervalTicks;
    }

    private static bool IsStationary(World.Entity entity) {
        if (!entity.TryGetComponent<VelocityComponent>(out var velocity)) {
            return false;
        }

        var speedSquared = velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z;
        return speedSquared <= StationarySpeedSquaredEpsilon;
    }

    private static bool HasSignificantChange(World.Entity entity, SentEntityState state) {
        var positionDeltaSquared = Vector3.DistanceSquared(entity.Position, state.Position);
        if (positionDeltaSquared >= PositionDeltaThreshold * PositionDeltaThreshold) {
            return true;
        }

        var velocity = ReadVelocity(entity);
        var velocityDeltaSquared = Vector3.DistanceSquared(velocity, state.Velocity);
        return velocityDeltaSquared >= VelocityDeltaThreshold * VelocityDeltaThreshold;
    }

    private static void UpsertSentState(Dictionary<int, SentEntityState> sentStates, World.Entity entity, long tick, bool fullSnapshot) {
        if (!sentStates.TryGetValue(entity.Id, out var state)) {
            state = new SentEntityState();
            sentStates[entity.Id] = state;
        }

        state.Position = entity.Position;
        state.Velocity = ReadVelocity(entity);
        state.LastSentTick = tick;
        if (fullSnapshot) {
            state.LastFullSentTick = tick;
        }
    }

    private static Vector3 ReadVelocity(World.Entity entity) {
        if (!entity.TryGetComponent<VelocityComponent>(out var velocity)) {
            return Vector3.Zero;
        }

        return new Vector3(velocity.X, velocity.Y, velocity.Z);
    }

    private void EnsureInterestGrid() {
        if (_interestGridBuiltAtTick == Tick) {
            return;
        }

        _interestGrid.Clear();
        foreach (var entity in World.Entities) {
            var key = GetGridCell(Flat(entity.Position));
            if (!_interestGrid.TryGetValue(key, out var entities)) {
                entities = [];
                _interestGrid[key] = entities;
            }

            entities.Add(entity);
        }

        _interestGridBuiltAtTick = Tick;
    }

    private IEnumerable<World.Entity> EnumerateNearbyEntities(Vector2 center, float radius) {
        var (centerX, centerY) = GetGridCell(center);
        var cellRadius = (int)MathF.Ceiling(radius / SnapshotGridCellSize);
        for (var y = centerY - cellRadius; y <= centerY + cellRadius; y++) {
            for (var x = centerX - cellRadius; x <= centerX + cellRadius; x++) {
                if (!_interestGrid.TryGetValue((x, y), out var entities)) {
                    continue;
                }

                foreach (var entity in entities) {
                    yield return entity;
                }
            }
        }
    }

    private static (int X, int Y) GetGridCell(Vector2 position) {
        var x = (int)MathF.Floor(position.X / SnapshotGridCellSize);
        var y = (int)MathF.Floor(position.Y / SnapshotGridCellSize);
        return (x, y);
    }

    private static Vector2 Flat(Vector3 position) => new(position.X, position.Z);
}
