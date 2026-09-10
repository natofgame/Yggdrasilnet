using System.Collections.Concurrent;
using Yggdrasilnet.Server.Services;
using Yggdrasilnet.Server.Simulation.Content;
using Yggdrasilnet.Server.Simulation.Managers;
using Yggdrasilnet.Server.Simulation.Snapshot;
using ContentEntity = Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.Session;
using Yggdrasilnet.Server.Utils;
using Yggdrasilnet.Shared.Network.Packet.Packets;

namespace Yggdrasilnet.Server.Simulation;

public sealed class Simulation : ISimulationContext {
    public ConcurrentQueue<ISimulationEvent> IncomingEvents { get; } = new();

    private readonly PlayerSessionRegistry _sessions = new();
    private readonly World.World _world;
    private readonly SnapshotBuilder _snapshotBuilder;

    private readonly DefinitionRegistry<ContentEntity.EntityDefinition> _entityDefinitions = new();
    private readonly EntitySpawnManager _entitySpawnManager;
    private readonly PlayerSimulationManager _playerSimulationManager;
    private readonly PacketDispatchManager _packetDispatchManager;

    private const string CrowdSizeEnvVar = "YGG_CROWD_SIZE";
    private const int DefaultCrowdSize = 0;

    private NetServer? _netServer;
    public NetServer? NetServer {
        get => _netServer;
        set {
            _netServer = value;
            _playerSimulationManager.NetServer = value;
        }
    }

    public long Tick { get; private set; }
    public PlayerSessionRegistry Sessions => _sessions;
    public World.World World => _world;

    public Simulation() {
        _world = new World.World();
        _world.Load();
        _snapshotBuilder = new SnapshotBuilder(_world);

        _entityDefinitions.Load(ContentPaths.Resolve("Entities"));
        var entityFactory = new EntityFactory();

        _entitySpawnManager = new EntitySpawnManager(_entityDefinitions, entityFactory, _world);
        _playerSimulationManager = new PlayerSimulationManager(_sessions, _entityDefinitions, entityFactory, _world);
        _packetDispatchManager = new PacketDispatchManager(_playerSimulationManager);

        SpawnEntities("crowd", ReadCrowdSizeFromEnv());
    }

    public void SpawnEntities(string definitionId, int count) => _entitySpawnManager.SpawnEntities(definitionId, count);

    private static int ReadCrowdSizeFromEnv() {
        var raw = Environment.GetEnvironmentVariable(CrowdSizeEnvVar);
        return int.TryParse(raw, out var count) ? count : DefaultCrowdSize;
    }

    public void Update(float deltaTime) {
        Tick++;

        _packetDispatchManager.DrainIncomingEvents(IncomingEvents, this, Tick);
        _world.Update(deltaTime);
    }

    public SnapshotPacket BuildSnapshotForSession(PlayerSession session, int tickRate, bool forceFullSnapshot) {
        return _snapshotBuilder.BuildSnapshotForSession(session, Tick, tickRate, forceFullSnapshot);
    }

    internal SnapshotBuilder.BroadcastBatch BeginSnapshotBatch() => _snapshotBuilder.BeginBroadcastBatch(Tick);
}
