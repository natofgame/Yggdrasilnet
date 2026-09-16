using LiteNetLib;
using Yggdrasilnet.Server.Simulation;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet.Packets;

namespace Yggdrasilnet.Server.Handlers;

public sealed class SpawnEntitiesHandler : IPacketHandler<SpawnEntitiesPacket, ISimulationContext> {
    public void Handle(NetPeer peer, SpawnEntitiesPacket packet, ISimulationContext context) {
        context.SpawnEntities(packet.DefinitionId, packet.Count);
    }
}
