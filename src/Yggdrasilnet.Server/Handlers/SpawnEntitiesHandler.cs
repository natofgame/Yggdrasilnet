using LiteNetLib;
using Yggdrasilnet.Server.Simulation;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet.Packets;

namespace Yggdrasilnet.Server.Handlers;

/// <summary>
/// Load-test/debug handler: lets a client ask the server to spawn extra entities at
/// runtime, so a load-test tool can ramp up entity count without restarting the server.
/// </summary>
public sealed class SpawnEntitiesHandler : IPacketHandler<SpawnEntitiesPacket, ISimulationContext> {
    public void Handle(NetPeer peer, SpawnEntitiesPacket packet, ISimulationContext context) {
        context.SpawnEntities(packet.DefinitionId, packet.Count);
    }
}
