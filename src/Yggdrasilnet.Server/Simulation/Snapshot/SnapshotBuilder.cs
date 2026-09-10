using System.Numerics;
using Yggdrasilnet.Server.Simulation.Session;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Network.Packet.Packets;
using Yggdrasilnet.Shared.Network.Packet.Snapshot;

namespace Yggdrasilnet.Server.Simulation.Snapshot;

public sealed class SnapshotBuilder(World.World world) {
    private readonly record struct SnapshotDistanceTier(float MaxDistance);
    private readonly record struct SnapshotDistanceTierSquared(float MaxDistanceSquared, int TargetHz);

    private const float SnapshotGridCellSize = 20f;
    private const int SnapshotBaseEntityBytes = 22;
    private const int SnapshotVelocityComponentBytes = 13;
    private const int MaxEntitiesPerSnapshot = 220;
    private const int MaxFarthestTierEntitiesPerSnapshot = 32;
    private const int SentStateTimeToLiveSeconds = 3;
    private const int MaxStationaryEntityHz = 2;
    private const float StationarySpeedSquaredEpsilon = 0.0001f;
    private const float PositionDeltaThreshold = 0.10f;
    private const float VelocityDeltaThreshold = 0.05f;
    private const float PositionDeltaThresholdSquared = PositionDeltaThreshold * PositionDeltaThreshold;
    private const float VelocityDeltaThresholdSquared = VelocityDeltaThreshold * VelocityDeltaThreshold;

    private static readonly SnapshotDistanceTier[] SnapshotDistanceTiers = [
        new(12f),
        new(28f),
        new(45f),
    ];

    private static readonly SnapshotDistanceTierSquared[] SnapshotDistanceTiersSquared = [
        new(12f * 12f, 20),
        new(28f * 28f, 6),
        new(45f * 45f, 1),
    ];

    private readonly Dictionary<long, List<World.Entity>> _interestGrid = new();
    private readonly Stack<List<World.Entity>> _interestBucketPool = new();
    private readonly Dictionary<long, List<World.Entity>> _nearbyEntitiesByCell = new();
    private readonly Stack<List<World.Entity>> _nearbyBufferPool = new();
    private readonly List<int> _sentStatePruneBuffer = new();
    private long _interestGridBuiltAtTick = -1;
    private readonly SnapshotPacket _broadcastPacket = new();
    private readonly Dictionary<int, EntitySnapshot> _broadcastEntities = new();
    private readonly List<EntitySnapshot> _broadcastSnapshotPool = new();
    private int _usedBroadcastSnapshots;
    private int _broadcastBatchVersion;
    private bool _broadcastBatchActive;

    internal BroadcastBatch BeginBroadcastBatch(long tick) {
        if (_broadcastBatchActive) {
            throw new InvalidOperationException("A snapshot broadcast batch is already active.");
        }

        _broadcastBatchActive = true;
        return new BroadcastBatch(this, tick, unchecked(++_broadcastBatchVersion));
    }

    internal readonly struct BroadcastBatch(SnapshotBuilder builder, long tick, int version) : IDisposable {
        public SnapshotPacket Build(PlayerSession session, int tickRate, bool forceFullSnapshot) {
            if (!builder._broadcastBatchActive || builder._broadcastBatchVersion != version) {
                throw new ObjectDisposedException(nameof(BroadcastBatch));
            }

            return builder.BuildSnapshot(session, tick, tickRate, forceFullSnapshot, true);
        }

        public void Dispose() {
            if (!builder._broadcastBatchActive || builder._broadcastBatchVersion != version) {
                return;
            }

            builder.EndBroadcastBatch();
        }
    }

    private void EndBroadcastBatch() {
        _broadcastPacket.Entities.Clear();
        _broadcastEntities.Clear();
        foreach (var snapshot in _broadcastSnapshotPool) {
            snapshot.Components.Clear();
        }
        var retainedCount = Math.Max(_usedBroadcastSnapshots, world.Entities.Count);
        if (retainedCount < _broadcastSnapshotPool.Count) {
            _broadcastSnapshotPool.RemoveRange(retainedCount, _broadcastSnapshotPool.Count - retainedCount);
        }
        _usedBroadcastSnapshots = 0;
        _broadcastBatchActive = false;
    }

    public SnapshotPacket BuildSnapshotForSession(PlayerSession session, long tick, int tickRate, bool forceFullSnapshot) {
        return BuildSnapshot(session, tick, tickRate, forceFullSnapshot, false);
    }

    private SnapshotPacket BuildSnapshot(PlayerSession session, long tick, int tickRate, bool forceFullSnapshot, bool broadcast) {
        var packet = broadcast ? _broadcastPacket : new SnapshotPacket();
        if (broadcast) {
            packet.Entities.Clear();
        }
        if (session.EntityId == -1) {
            return packet;
        }

        if (!world.TryGetEntity(session.EntityId, out var ownerEntity)) {
            return packet;
        }

        var observerPosition = Flat(ownerEntity.Position);
        var ownerVelocity = ReadVelocity(ownerEntity, out _);
        packet.Entities.Add(GetEntitySnapshot(ownerEntity, broadcast));
        var sentStates = session.LastSentEntities;
        UpsertSentState(sentStates, ownerEntity, ownerVelocity, tick, forceFullSnapshot);

        EnsureInterestGrid(tick);
        var maxDistance = SnapshotDistanceTiers[^1].MaxDistance;
        var maxDistanceSquared = maxDistance * maxDistance;
        var farthestTierHz = SnapshotDistanceTiersSquared[^1].TargetHz;
        var sentEntityLimit = Math.Max(1, MaxEntitiesPerSnapshot);
        var sentEntityCount = 1;
        var farthestTierSentCount = 0;
        var reachedEntityLimit = false;
        var nearbyEntities = GetNearbyEntities(observerPosition, maxDistance);
        var nearbyCount = nearbyEntities.Count;
        if (nearbyCount > 0) {
            var safeStartIndex = session.SnapshotRoundRobinOffset % nearbyCount;
            if (safeStartIndex < 0) {
                safeStartIndex += nearbyCount;
            }
            session.SnapshotRoundRobinOffset = (safeStartIndex + sentEntityLimit) % nearbyCount;

            for (var i = 0; i < nearbyCount; i++) {
                if (sentEntityCount >= sentEntityLimit) {
                    reachedEntityLimit = true;
                    break;
                }

                var index = safeStartIndex + i;
                if (index >= nearbyCount) {
                    index -= nearbyCount;
                }

                var entity = nearbyEntities[index];
                if (entity.Id == ownerEntity.Id) {
                    continue;
                }

                var distanceSquared = Vector2.DistanceSquared(observerPosition, Flat(entity.Position));
                if (distanceSquared > maxDistanceSquared) {
                    continue;
                }

                var targetHz = ResolveTargetHz(distanceSquared);
                if (targetHz <= 0) {
                    continue;
                }

                var entityVelocity = ReadVelocity(entity, out var hasVelocity);
                sentStates.TryGetValue(entity.Id, out var state);
                if (state != null) {
                    state.LastObservedTick = tick;
                }

                if (hasVelocity && IsStationary(entityVelocity)) {
                    targetHz = Math.Min(targetHz, MaxStationaryEntityHz);
                }

                if (targetHz == farthestTierHz && farthestTierSentCount >= MaxFarthestTierEntitiesPerSnapshot) {
                    continue;
                }

                if (!forceFullSnapshot && !IsEntityDueForSend(state, targetHz, tickRate, tick)) {
                    continue;
                }

                if (!forceFullSnapshot && state != null && !HasSignificantChange(entity.Position, entityVelocity, state)) {
                    continue;
                }

                packet.Entities.Add(GetEntitySnapshot(entity, broadcast));
                UpsertSentState(sentStates, entity, entityVelocity, tick, forceFullSnapshot);
                sentEntityCount++;
                if (targetHz == farthestTierHz) {
                    farthestTierSentCount++;
                }
            }
        }

        if (forceFullSnapshot && !reachedEntityLimit) {
            PruneStaleSentStates(sentStates, ownerEntity.Id, tick, tickRate);
        }

        return packet;
    }

    private EntitySnapshot GetEntitySnapshot(World.Entity entity, bool broadcast) {
        if (broadcast && _broadcastEntities.TryGetValue(entity.Id, out var cached)) {
            return cached;
        }

        EntitySnapshot snapshot;
        if (broadcast) {
            if (_usedBroadcastSnapshots == _broadcastSnapshotPool.Count) {
                _broadcastSnapshotPool.Add(new EntitySnapshot());
            }
            snapshot = _broadcastSnapshotPool[_usedBroadcastSnapshots++];
        } else {
            snapshot = new EntitySnapshot();
        }
        snapshot.EntityId = entity.Id;
        snapshot.PositionX = entity.Position.X;
        snapshot.PositionY = entity.Position.Y;
        snapshot.PositionZ = entity.Position.Z;
        snapshot.EstimatedBytes = SnapshotBaseEntityBytes;
        snapshot.LastInputSequence = 0;

        foreach (var component in entity.Components) {
            if (component is INetworkedComponent networked) {
                snapshot.Components.Add(networked);
                snapshot.EstimatedBytes += networked.Type switch {
                    NetworkedComponentType.Velocity => SnapshotVelocityComponentBytes,
                    _ => 0
                };
            }
        }

        if (entity.TryGetComponent<InputComponent>(out var input)) {
            snapshot.LastInputSequence = input.LastSequence;
        }

        if (broadcast) {
            _broadcastEntities.Add(entity.Id, snapshot);
        }
        return snapshot;
    }

    private static int ResolveTargetHz(float distanceSquared) {
        for (var i = 0; i < SnapshotDistanceTiersSquared.Length; i++) {
            var tier = SnapshotDistanceTiersSquared[i];
            if (distanceSquared <= tier.MaxDistanceSquared) {
                return tier.TargetHz;
            }
        }

        return 0;
    }

    private static bool IsEntityDueForSend(SentEntityState? state, int targetHz, int tickRate, long currentTick) {
        if (state == null) {
            return true;
        }

        var safeTickRate = Math.Max(1, tickRate);
        var safeHz = Math.Max(1, targetHz);
        var intervalTicks = Math.Max(1, (int)MathF.Round(safeTickRate / (float)safeHz));
        return currentTick - state.LastSentTick >= intervalTicks;
    }

    private static bool IsStationary(Vector3 velocity) {
        var speedSquared = velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z;
        return speedSquared <= StationarySpeedSquaredEpsilon;
    }

    private static bool HasSignificantChange(Vector3 position, Vector3 velocity, SentEntityState state) {
        var positionDeltaSquared = Vector3.DistanceSquared(position, state.Position);
        if (positionDeltaSquared >= PositionDeltaThresholdSquared) {
            return true;
        }

        var velocityDeltaSquared = Vector3.DistanceSquared(velocity, state.Velocity);
        return velocityDeltaSquared >= VelocityDeltaThresholdSquared;
    }

    private static void UpsertSentState(Dictionary<int, SentEntityState> sentStates, World.Entity entity, Vector3 velocity, long tick, bool fullSnapshot) {
        if (!sentStates.TryGetValue(entity.Id, out var state)) {
            state = new SentEntityState();
            sentStates[entity.Id] = state;
        }

        state.Position = entity.Position;
        state.Velocity = velocity;
        state.LastSentTick = tick;
        state.LastObservedTick = tick;
        if (fullSnapshot) {
            state.LastFullSentTick = tick;
        }
    }

    private static Vector3 ReadVelocity(World.Entity entity, out bool hasVelocity) {
        if (!entity.TryGetComponent<VelocityComponent>(out var velocity)) {
            hasVelocity = false;
            return Vector3.Zero;
        }

        hasVelocity = true;
        return new Vector3(velocity.X, velocity.Y, velocity.Z);
    }

    private void EnsureInterestGrid(long tick) {
        if (_interestGridBuiltAtTick == tick) {
            return;
        }

        RecycleBuffers(_interestGrid, _interestBucketPool);
        RecycleBuffers(_nearbyEntitiesByCell, _nearbyBufferPool);

        foreach (var entity in world.Entities) {
            var key = GetGridCellKey(Flat(entity.Position));
            if (!_interestGrid.TryGetValue(key, out var entities)) {
                entities = _interestBucketPool.TryPop(out var bucket) ? bucket : new List<World.Entity>();
                _interestGrid[key] = entities;
            }

            entities.Add(entity);
        }

        _interestGridBuiltAtTick = tick;
    }

    private static void RecycleBuffers(Dictionary<long, List<World.Entity>> buffers, Stack<List<World.Entity>> pool) {
        // Keep only the previous tick's working set, never historical empty cells.
        pool.Clear();
        foreach (var buffer in buffers.Values) {
            buffer.Clear();
            pool.Push(buffer);
        }
        buffers.Clear();
    }

    private void PruneStaleSentStates(Dictionary<int, SentEntityState> sentStates, int ownerEntityId, long tick, int tickRate) {
        var ttlTicks = Math.Max(1, tickRate * SentStateTimeToLiveSeconds);
        _sentStatePruneBuffer.Clear();
        foreach (var entry in sentStates) {
            if (entry.Key == ownerEntityId) {
                continue;
            }

            if (tick - entry.Value.LastObservedTick > ttlTicks) {
                _sentStatePruneBuffer.Add(entry.Key);
            }
        }

        for (var i = 0; i < _sentStatePruneBuffer.Count; i++) {
            sentStates.Remove(_sentStatePruneBuffer[i]);
        }
    }

    private List<World.Entity> GetNearbyEntities(Vector2 center, float radius) {
        var (centerX, centerY) = GetGridCell(center);
        var key = GetGridCellKey(centerX, centerY);
        if (_nearbyEntitiesByCell.TryGetValue(key, out var cached)) {
            return cached;
        }
        var entitiesBuffer = _nearbyBufferPool.TryPop(out var buffer) ? buffer : new List<World.Entity>();
        var cellRadius = (int)MathF.Ceiling(radius / SnapshotGridCellSize);
        for (var y = centerY - cellRadius; y <= centerY + cellRadius; y++) {
            for (var x = centerX - cellRadius; x <= centerX + cellRadius; x++) {
                if (!_interestGrid.TryGetValue(GetGridCellKey(x, y), out var entities)) {
                    continue;
                }

                entitiesBuffer.AddRange(entities);
            }
        }
        _nearbyEntitiesByCell.Add(key, entitiesBuffer);
        return entitiesBuffer;
    }

    private static (int X, int Y) GetGridCell(Vector2 position) {
        var x = (int)MathF.Floor(position.X / SnapshotGridCellSize);
        var y = (int)MathF.Floor(position.Y / SnapshotGridCellSize);
        return (x, y);
    }

    private static long GetGridCellKey(Vector2 position) {
        var (x, y) = GetGridCell(position);
        return GetGridCellKey(x, y);
    }

    private static long GetGridCellKey(int x, int y) {
        return ((long)x << 32) | (uint)y;
    }

    private static Vector2 Flat(Vector3 position) => new(position.X, position.Z);
}
