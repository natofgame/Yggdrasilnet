using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Packets;

public sealed class InputPacket : IPacket {
    public PacketType PacketType => PacketType.Input;
    
    public uint Sequence { get; set; }
    public float MoveX { get; set; }
    public float MoveZ { get; set; }
    public float DeltaTime { get; set; }
    
    public void Serialize(NetDataWriter writer) {
        writer.Put(Sequence);
        writer.Put(MoveX);
        writer.Put(MoveZ);
        writer.Put(DeltaTime);
    }
    public void Deserialize(NetDataReader reader) {
        Sequence = reader.GetUInt();
        MoveX = reader.GetFloat();
        MoveZ = reader.GetFloat();
        DeltaTime = reader.GetFloat();
    }
}