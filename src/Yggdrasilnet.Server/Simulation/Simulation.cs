using System.Collections.Concurrent;
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
    public ConcurrentQueue<ISimulationEvent> IncomingEvents { get; } = new();

    private readonly PacketDispatcher<ISimulationContext> _dispatcher = new();
    private readonly PlayerSessionRegistry _sessions = new();
    private readonly World.World _world;

    private readonly DefinitionRegistry<ContentEntity.EntityDefinition> _entityDefinitions = new();
    private readonly EntityFactory _entityFactory = new();

    public NetServer? NetServer { get; set; }
    
    public long Tick { get; private set; }
    public PlayerSessionRegistry Sessions => _sessions;
    public World.World World => _world;

    public Simulation() {
        _dispatcher.Register(PacketType.PlayerConnexion, new PlayerConnexionHandler());
        _dispatcher.Register(PacketType.Input, new InputHandler());
        
        _world = new World.World();
        _world.Load();

        _entityDefinitions.Load(ContentPaths.Resolve("Entities"));

        SpawnTestMonsters();
    }

    private void SpawnTestMonsters() {
        if (_entityDefinitions.TryGet("goblin", out var goblinDefinition)) {
            _entityFactory.Create(goblinDefinition, World, new Vector3(3f, 0f, 0f));
        } else {
            Log.Warning("Entity definition 'goblin' not found");
        }
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

    public SnapshotPacket BuildSnapshot() {
        var packet = new SnapshotPacket();
        foreach (var entity in World.Entities) {
            var snapshot = new EntitySnapshot {
                EntityId =  entity.Id,
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
            
            packet.Entities.Add(snapshot);
        }

        return packet;
    }
}
