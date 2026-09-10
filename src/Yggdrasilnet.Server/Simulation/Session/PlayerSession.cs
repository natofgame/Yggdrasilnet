using System.Collections.Generic;
using System.Numerics;
using LiteNetLib;

namespace Yggdrasilnet.Server.Simulation.Session;

public sealed class SentEntityState {
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public long LastSentTick { get; set; }
    public long LastObservedTick { get; set; }
    public long LastFullSentTick { get; set; }
}

public class PlayerSession(NetPeer peer, long connectedAtTick) {
    public readonly NetPeer Peer = peer;
    public readonly int Id = peer.Id;
    public readonly long ConnectedAtTick = connectedAtTick;
    public int EntityId { get; set; } = -1;
    public Dictionary<int, SentEntityState> LastSentEntities { get; } = new();
    public int SnapshotRoundRobinOffset { get; set; }
}