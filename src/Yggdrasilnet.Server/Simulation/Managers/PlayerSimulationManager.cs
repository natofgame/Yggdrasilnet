using System.Numerics;
using LiteNetLib;
using Serilog;
using Yggdrasilnet.Server.Services;
using Yggdrasilnet.Server.Simulation.Content;
using Yggdrasilnet.Server.Simulation.Session;
using Yggdrasilnet.Shared.Network.Packet.Packets;
using ContentEntity = Yggdrasilnet.Server.Simulation.Content.Entity;

namespace Yggdrasilnet.Server.Simulation.Managers;

public sealed class PlayerSimulationManager(
    PlayerSessionRegistry sessions,
    DefinitionRegistry<ContentEntity.EntityDefinition> entityDefinitions,
    EntityFactory entityFactory,
    World.World world
) {
    public NetServer? NetServer { get; set; }

    public void CreatePlayer(NetPeer peer, long tick) {
        var session = sessions.Create(peer, tick);

        if (!entityDefinitions.TryGet("player", out var playerDefinition)) {
            Log.Warning("Entity definition 'player' not found, disconnecting {EndPoint}", peer.Address);
            peer.Disconnect();
            return;
        }

        var entity = entityFactory.Create(playerDefinition, world, Vector3.Zero);
        session.EntityId = entity.Id;

        NetServer?.Send(peer, new PlayerConnexionPacket { EntityId = entity.Id, IsOwner = true });
        Log.Information("Player {PlayerId} connected: {EndPoint} entity: {EntityId}", session.Id, peer.Address, entity.Id);
    }

    public void RemovePlayer(NetPeer peer, DisconnectInfo info) {
        if (!sessions.Remove(peer, out var session)) {
            return;
        }

        if (session != null) {
            var id = session.EntityId;
            if (id == -1) {
                return;
            }
            world.Despawn(id);
        }

        Log.Information("Player {PlayerId} disconnected: {EndPoint} ({Reason})", session!.Id, peer.Address, info.Reason);
    }
}
