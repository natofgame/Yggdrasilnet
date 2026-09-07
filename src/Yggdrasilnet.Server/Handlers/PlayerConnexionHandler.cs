using LiteNetLib;
using Serilog;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet.Packets;

namespace Yggdrasilnet.Server.Handlers;

public sealed class PlayerConnexionHandler : IPacketHandler<PlayerConnexionPacket> {
    public void Handle(NetPeer peer, PlayerConnexionPacket packet) {
        Log.Information("Player {PlayerId} announced itself (owner={IsOwner})", packet.PlayerId, packet.IsOwner);
    }
}
