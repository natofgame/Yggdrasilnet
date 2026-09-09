namespace Yggdrasilnet.Shared.Network.Packet;

public enum PacketType : byte {
    Snapshot = 0, 
    PlayerConnexion = 1,
    Input = 2,
    SpawnEntities = 3,
    Stats = 4,
    SnapshotChunk = 5,
}
