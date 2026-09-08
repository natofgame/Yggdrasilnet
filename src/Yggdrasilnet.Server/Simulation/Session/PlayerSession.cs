using LiteNetLib;

namespace Yggdrasilnet.Server.Simulation.Session;

public class PlayerSession(NetPeer peer, long connectedAtTick) {
    public readonly NetPeer Peer = peer;
    public readonly int Id = peer.Id;
    public readonly long ConnectedAtTick = connectedAtTick;
}