using System.Collections.Concurrent;
using Serilog;
using Yggdrasilnet.Server.Handlers;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Server.Simulation.Managers;

public sealed class PacketDispatchManager {
    private readonly PacketDispatcher<ISimulationContext> _dispatcher = new();
    private readonly PlayerSimulationManager _playerSimulation;

    public PacketDispatchManager(PlayerSimulationManager playerSimulation) {
        _playerSimulation = playerSimulation;

        _dispatcher.Register(PacketType.PlayerConnexion, new PlayerConnexionHandler());
        _dispatcher.Register(PacketType.Input, new InputHandler());
        _dispatcher.Register(PacketType.SpawnEntities, new SpawnEntitiesHandler());
    }

    public void DrainIncomingEvents(ConcurrentQueue<ISimulationEvent> incomingEvents, ISimulationContext context, long tick) {
        while (incomingEvents.TryDequeue(out var simulationEvent)) {
            switch (simulationEvent) {
                case PeerConnectedEvent peerConnected:
                    _playerSimulation.CreatePlayer(peerConnected.Peer, tick);
                    break;
                case PeerDisconnectedEvent peerDisconnected:
                    _playerSimulation.RemovePlayer(peerDisconnected.Peer, peerDisconnected.Info);
                    break;
                case PacketReceivedEvent packetReceived:
                    if (!_dispatcher.Dispatch(packetReceived.Peer, packetReceived.Packet, context)) {
                        Log.Warning("No handler registered for packet {PacketType} from {EndPoint}",
                            packetReceived.Packet.PacketType, packetReceived.Peer.Address);
                    }
                    break;
            }
        }
    }
}
