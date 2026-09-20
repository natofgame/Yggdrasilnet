using LiteNetLib;
using Yggdrasilnet.Network.Packet;
using Yggdrasilnet.Network.Packet.Packets;
using Yggdrasilnet.Server.Simulation;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Handlers;

public sealed class InputHandler : IPacketHandler<InputPacket, ISimulationContext> {
    public void Handle(NetPeer peer, InputPacket packet, ISimulationContext context) {
        if (!context.Sessions.TryGet(peer, out var session) || session!.EntityId == -1) {
            return;
        }

        if (!context.World.TryGetEntity(session.EntityId, out var entity)) {
            return;
        }

        if (!entity.TryGetComponent<InputComponent>(out var input)) {
            return;
        }
        
        input.ApplyInput(packet.MoveX, packet.MoveZ, packet.Sequence);
    }
}