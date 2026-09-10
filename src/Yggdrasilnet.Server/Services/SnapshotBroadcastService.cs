using LiteNetLib;
using Yggdrasilnet.Shared.Network.Packet.Packets;
using Yggdrasilnet.Shared.Network.Packet.Snapshot;

namespace Yggdrasilnet.Server.Services;

public sealed class SnapshotBroadcastService(NetServer netServer, Simulation.Simulation simulation, int tickRate) {
    private const int KeyframeIntervalSeconds = 1;
    private const int SnapshotChunkTargetBytes = 1000;
    private const int SnapshotChunkHeaderBytes = 10;
    private const int SnapshotChunkSafetyMarginBytes = 96;
    private const int SnapshotBaseEntityBytes = 22;
    private const int SnapshotVelocityComponentBytes = 13;

    private uint _snapshotFrameId;
    private readonly SnapshotChunkPacket _chunk = new();

    public SnapshotBroadcastMetrics Broadcast() {
        var sessions = simulation.Sessions.All;
        if (sessions.Count == 0) {
            return default;
        }

        var frameId = unchecked(++_snapshotFrameId);
        var keyframeIntervalTicks = Math.Max(1, tickRate * KeyframeIntervalSeconds);
        var isKeyframeTick = simulation.Tick % keyframeIntervalTicks == 0;
        var metrics = new SnapshotBroadcastMetrics {
            Sessions = sessions.Count,
            Keyframe = isKeyframeTick
        };

        using var batch = simulation.BeginSnapshotBatch();
        foreach (var session in sessions) {
            var snapshot = batch.Build(session, tickRate, isKeyframeTick);
            metrics.SnapshotEntities += snapshot.Entities.Count;
            if (snapshot.Entities.Count == 0) {
                continue;
            }

            SendSnapshotChunks(session.Peer, snapshot.Entities, frameId, isKeyframeTick, ref metrics);
        }

        return metrics;
    }

    private void SendSnapshotChunks(NetPeer peer, List<EntitySnapshot> entities, uint frameId, bool keyframeTick, ref SnapshotBroadcastMetrics metrics) {
        if (entities.Count == 0) {
            return;
        }

        var delivery = keyframeTick ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;
        var chunkTargetBytes = ResolveChunkTargetBytes(peer, delivery);
        var maxEntityBudget = chunkTargetBytes - SnapshotChunkHeaderBytes;

        var currentChunk = _chunk;
        currentChunk.FrameId = frameId;
        currentChunk.ChunkIndex = 0;
        currentChunk.IsLastChunk = false;
        currentChunk.Entities.Clear();
        var currentBytes = SnapshotChunkHeaderBytes;

        try {
            foreach (var entity in entities) {
                var entityBytes = EstimateEntityBytes(entity);
                if (entityBytes > maxEntityBudget) {
                    metrics.DroppedOversizedEntities++;
                    continue;
                }

                var wouldOverflow = currentChunk.Entities.Count > 0
                                    && currentBytes + entityBytes > chunkTargetBytes;
                if (wouldOverflow) {
                    SendChunk(peer, currentChunk, delivery, ref metrics);
                    currentChunk.Entities.Clear();
                    currentChunk.ChunkIndex++;
                    currentBytes = SnapshotChunkHeaderBytes;
                }

                currentChunk.Entities.Add(entity);
                currentBytes += entityBytes;
            }

            if (currentChunk.Entities.Count > 0) {
                currentChunk.IsLastChunk = true;
                SendChunk(peer, currentChunk, delivery, ref metrics);
            }
        } finally {
            currentChunk.Entities.Clear();
        }
    }

    private static int ResolveChunkTargetBytes(NetPeer peer, DeliveryMethod delivery) {
        var maxPacketSize = peer.GetMaxSinglePacketSize(delivery);
        var safeTarget = maxPacketSize - SnapshotChunkSafetyMarginBytes;
        if (safeTarget <= SnapshotChunkHeaderBytes) {
            return SnapshotChunkHeaderBytes + 1;
        }

        return Math.Min(SnapshotChunkTargetBytes, safeTarget);
    }

    private void SendChunk(NetPeer peer, SnapshotChunkPacket chunk, DeliveryMethod delivery, ref SnapshotBroadcastMetrics metrics) {
        netServer.Send(peer, chunk, delivery);
        metrics.ChunksSent++;
    }

    private static int EstimateEntityBytes(EntitySnapshot entity) {
        if (entity.EstimatedBytes > 0) {
            return entity.EstimatedBytes;
        }

        var size = SnapshotBaseEntityBytes;
        foreach (var component in entity.Components) {
            size += component.Type switch {
                NetworkedComponentType.Velocity => SnapshotVelocityComponentBytes,
                _ => 0
            };
        }

        return size;
    }
}

public struct SnapshotBroadcastMetrics {
    public int Sessions { get; set; }
    public int SnapshotEntities { get; set; }
    public int ChunksSent { get; set; }
    public int DroppedOversizedEntities { get; set; }
    public bool Keyframe { get; set; }
}
