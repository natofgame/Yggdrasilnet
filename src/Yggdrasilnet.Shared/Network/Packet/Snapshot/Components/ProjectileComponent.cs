using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Snapshot.Components;

public class ProjectileComponent : INetworkedComponent {
    public NetworkedComponentType Type => NetworkedComponentType.Projectile;

    public string SpellId { get; set; } = string.Empty;
    public int CasterEntityId { get; set; }
    public int TargetEntityId { get; set; }

    public void Serialize(NetDataWriter writer) {
        writer.Put(SpellId);
        writer.Put(CasterEntityId);
        writer.Put(TargetEntityId);
    }

    public void Deserialize(NetDataReader reader) {
        SpellId = reader.GetString();
        CasterEntityId = reader.GetInt();
        TargetEntityId = reader.GetInt();
    }
}
