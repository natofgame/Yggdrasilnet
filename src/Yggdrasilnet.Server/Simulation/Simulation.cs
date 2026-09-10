using System.Collections.Concurrent;
using System.Numerics;
using LiteNetLib;
using Serilog;
using Yggdrasilnet.Server.Handlers;
using Yggdrasilnet.Server.Simulation.Content;
using Yggdrasilnet.Server.Simulation.Snapshot;
using ContentEntity = Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.Session;
using Yggdrasilnet.Server.Utils;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet;
using Yggdrasilnet.Shared.Network.Packet.Packets;

namespace Yggdrasilnet.Server.Simulation;

public sealed class Simulation : ISimulationContext {
    public ConcurrentQueue<ISimulationEvent> IncomingEvents { get; } = new();

    private readonly PacketDispatcher<ISimulationContext> _dispatcher = new();
    private readonly PlayerSessionRegistry _sessions = new();
    private readonly World.World _world;
    private readonly SnapshotBuilder _snapshotBuilder;

    private readonly DefinitionRegistry<ContentEntity.EntityDefinition> _entityDefinitions = new();
    private readonly EntityFactory _entityFactory = new();

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
        _snapshotBuilder = new SnapshotBuilder(_world);

        _entityDefinitions.Load(ContentPaths.Resolve("Entities"));

        SpawnEntities("crowd", ReadCrowdSizeFromEnv());
    }

    private const string CrowdSizeEnvVar = "YGG_CROWD_SIZE";
    private const int DefaultCrowdSize = 0;
    private const float CrowdAreaSize = 30f;
    private const float DefaultCrowdSpawnSpacing = 4f;
    private const int MaxEntitiesPerSpawnRequest = 20_000;
    
    public void SpawnEntities(string definitionId, int count) {
        count = Math.Clamp(count, 0, MaxEntitiesPerSpawnRequest);
        if (count <= 0) {
            return;
        }

        if (!_entityDefinitions.TryGet(definitionId, out var definition)) {
            Log.Warning("Entity definition '{DefinitionId}' not found, cannot spawn {Count} entities", definitionId, count);
            return;
        }

        var random = new Random();
        var spawnPositions = BuildSpawnPositions(definitionId, definition, count, random);
        for (var i = 0; i < spawnPositions.Count; i++) {
            var position = spawnPositions[i];
            _entityFactory.Create(definition, World, position);
        }

        Log.Information("Spawned {Count} '{DefinitionId}' entities ({Total} total entities now)",
            count, definitionId, World.Entities.Count);
    }

    private static int ReadCrowdSizeFromEnv() {
        var raw = Environment.GetEnvironmentVariable(CrowdSizeEnvVar);
        return int.TryParse(raw, out var count) ? count : DefaultCrowdSize;
    }

    private static List<Vector3> BuildSpawnPositions(string definitionId, ContentEntity.EntityDefinition definition, int count, Random random) {
        if (count <= 0) {
            return [];
        }

        if (!string.Equals(definitionId, "crowd", StringComparison.OrdinalIgnoreCase)) {
            return BuildRandomSquarePositions(count, CrowdAreaSize, random);
        }

        var spacing = ResolveCrowdSpawnSpacing(definition);
        return BuildSpacedGridPositions(count, spacing);
    }

    private static float ResolveCrowdSpawnSpacing(ContentEntity.EntityDefinition definition) {
        foreach (var component in definition.Components) {
            if (component is ContentEntity.SteeringComponentDefinition steering && steering.SpawnSpacing > 0f) {
                return steering.SpawnSpacing;
            }
        }

        return DefaultCrowdSpawnSpacing;
    }

    private static List<Vector3> BuildRandomSquarePositions(int count, float areaSize, Random random) {
        var positions = new List<Vector3>(count);
        var half = areaSize / 2f;
        for (var i = 0; i < count; i++) {
            positions.Add(new Vector3(
                (float)(random.NextDouble() * 2f - 1f) * half,
                0f,
                (float)(random.NextDouble() * 2f - 1f) * half
            ));
        }

        return positions;
    }

    private static List<Vector3> BuildSpacedGridPositions(int count, float spacing) {
        var safeSpacing = MathF.Max(0.5f, spacing);
        var side = (int)MathF.Ceiling(MathF.Sqrt(count));
        var half = ((side - 1) * safeSpacing) / 2f;
        var positions = new List<Vector3>(count);
        for (var index = 0; index < count; index++) {
            var x = index % side;
            var z = index / side;
            positions.Add(new Vector3(
                x * safeSpacing - half,
                0f,
                z * safeSpacing - half
            ));
        }

        return positions;
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
        return _snapshotBuilder.BuildSnapshotForSession(session, Tick, tickRate, forceFullSnapshot);
    }

    internal SnapshotBuilder.BroadcastBatch BeginSnapshotBatch() => _snapshotBuilder.BeginBroadcastBatch(Tick);
}
