using System.Numerics;
using LiteNetLib;
using Yggdrasilnet.Gameplay.Enums;

namespace Yggdrasilnet.Server.Simulation.Session;

public readonly record struct SentHealthState(float Current, float Max);

public readonly record struct SentActionState(
    byte ActionType, SpellPhase Phase, float PhaseProgress01, string SpellId, int TargetEntityId, SpellType SpellType);

public sealed class SentEntityState {
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public SentHealthState? Health { get; set; }
    public SentActionState? Action { get; set; }
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