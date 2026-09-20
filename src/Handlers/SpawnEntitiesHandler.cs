using LiteNetLib;
using Yggdrasilnet.Network.Packet;
using Yggdrasilnet.Network.Packet.Packets;
using Yggdrasilnet.Server.Simulation;

namespace Yggdrasilnet.Server.Handlers;

public sealed class SpawnEntitiesHandler : IPacketHandler<SpawnEntitiesPacket, ISimulationContext> {
    public void Handle(NetPeer peer, SpawnEntitiesPacket packet, ISimulationContext context) {
        context.SpawnEntities(packet.DefinitionId, packet.Count);
    }
}
