using System.Collections.Concurrent;
using LiteNetLib;
using Serilog;
using Yggdrasilnet.Server.Handlers;
using Yggdrasilnet.Server.Simulation.Session;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet;
using Yggdrasilnet.Shared.Network.Packet.Packets;

namespace Yggdrasilnet.Server.Simulation;

public sealed class Simulation : ISimulationContext {
    public ConcurrentQueue<ISimulationEvent> IncomingEvents { get; } = new();

    private readonly PacketDispatcher<ISimulationContext> _dispatcher = new();
    private readonly PlayerSessionRegistry _sessions = new();
    private readonly Level _level = new();

    public long Tick { get; private set; }
    public PlayerSessionRegistry Sessions => _sessions;

    public Simulation() {
        _dispatcher.Register(PacketType.PlayerConnexion, new PlayerConnexionHandler());
    }

    public void Update(float deltaTime) {
        Tick++;

        DrainIncomingEvents();
        _level.Update(deltaTime);
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
        Log.Information("Player {PlayerId} connected: {EndPoint}", session.Id, peer.Address);
    }

    private void RemovePlayer(NetPeer peer, DisconnectInfo info) {
        if (!_sessions.Remove(peer, out var session)) {
            return;
        }

        Log.Information("Player {PlayerId} disconnected: {EndPoint} ({Reason})", session!.Id, peer.Address, info.Reason);
    }
}
