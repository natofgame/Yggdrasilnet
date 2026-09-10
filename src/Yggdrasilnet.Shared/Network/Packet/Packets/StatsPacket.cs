using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Packets;

/// <summary>
/// Server -> client telemetry broadcast, sent periodically alongside snapshots so
/// load-test tools can read real server-side perf numbers instead of scraping logs.
/// </summary>
public sealed class StatsPacket : IPacket {
    public PacketType PacketType => PacketType.Stats;

    public float ActualTps { get; set; }
    public int TargetTps { get; set; }
    public int EntityCount { get; set; }
    public int PlayerCount { get; set; }
    public float AvgTickMs { get; set; }
    public float MaxTickMs { get; set; }
    public float BudgetMs { get; set; }

    public void Serialize(NetDataWriter writer) {
        writer.Put(ActualTps);
        writer.Put(TargetTps);
        writer.Put(EntityCount);
        writer.Put(PlayerCount);
        writer.Put(AvgTickMs);
        writer.Put(MaxTickMs);
        writer.Put(BudgetMs);
    }

    public void Deserialize(NetDataReader reader) {
        ActualTps = reader.GetFloat();
        TargetTps = reader.GetInt();
        EntityCount = reader.GetInt();
        PlayerCount = reader.GetInt();
        AvgTickMs = reader.GetFloat();
        MaxTickMs = reader.GetFloat();
        BudgetMs = reader.GetFloat();
    }
}
