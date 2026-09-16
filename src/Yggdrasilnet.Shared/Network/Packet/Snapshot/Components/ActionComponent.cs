using LiteNetLib.Utils;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Shared.Network.Packet.Snapshot.Components;

public class ActionComponent : INetworkedComponent {
    public NetworkedComponentType Type => NetworkedComponentType.Action;
    
    public byte ActionType { get; set; }
    public SpellPhase Phase { get; set; }
    public float PhaseProgress01 { get; set; }
    public string SpellId { get; set; } = string.Empty;
    public int TargetEntityId { get; set; }
    public SpellType SpellType { get; set; }
    
    public void Serialize(NetDataWriter writer) {
        writer.Put(ActionType);
        writer.Put((byte)Phase);
        writer.Put(PhaseProgress01);
        writer.Put(SpellId);
        writer.Put(TargetEntityId);
        writer.Put((byte)SpellType);
    }
    public void Deserialize(NetDataReader reader) {
        ActionType = reader.GetByte();
        Phase = (SpellPhase)reader.GetByte();
        PhaseProgress01 = reader.GetFloat();
        SpellId = reader.GetString();
        TargetEntityId = reader.GetInt();
        SpellType = (SpellType)reader.GetByte();
    }
}