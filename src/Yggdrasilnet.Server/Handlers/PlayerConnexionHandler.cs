using LiteNetLib;
using Serilog;
using Yggdrasilnet.Server.Simulation;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet.Packets;

namespace Yggdrasilnet.Server.Handlers;

public sealed class PlayerConnexionHandler : IPacketHandler<PlayerConnexionPacket, ISimulationContext> {
    public void Handle(NetPeer peer, PlayerConnexionPacket packet, ISimulationContext context) {
        Log.Information("Player {PlayerId} announced itself (owner={IsOwner})", packet.PlayerId, packet.IsOwner);
    }
}
