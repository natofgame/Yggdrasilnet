using LiteNetLib;
using Yggdrasilnet.Network.Packet;
using Yggdrasilnet.Network.Packet.Packets;
using Yggdrasilnet.Server.Simulation;

namespace Yggdrasilnet.Server.Handlers;

public sealed class PlayerConnexionHandler : IPacketHandler<PlayerConnexionPacket, ISimulationContext> {
    public void Handle(NetPeer peer, PlayerConnexionPacket packet, ISimulationContext context) {
       
    }
}
