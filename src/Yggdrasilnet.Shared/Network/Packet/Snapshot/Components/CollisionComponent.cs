using System.Numerics;
using LiteNetLib.Utils;
using Yggdrasilnet.Shared.Compatibility;
using Yggdrasilnet.Shared.Maths;

namespace Yggdrasilnet.Shared.Network.Packet.Snapshot.Components;

public class CollisionComponent : INetworkedComponent {
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public bool IsTrigger { get; set; }
    public CollisionLayer Layer { get; set; } = CollisionLayer.World;
    public CollisionLayer Mask { get; set; } = CollisionLayer.All;

    public Vector3 Size => new(X, Y, Z);
    
    public void Serialize(NetDataWriter writer) {
        writer.Put(X);
        writer.Put(Y);
        writer.Put(Z);
        writer.Put(IsTrigger);
        writer.PutEnum(Layer);
        writer.PutEnum(Mask);
    }
    
    public void Deserialize(NetDataReader reader) {
        X = reader.GetFloat();
        Y = reader.GetFloat();
        Z = reader.GetFloat();
        IsTrigger = reader.GetBool();
        Layer = reader.GetEnum<CollisionLayer>();
        Mask = reader.GetEnum<CollisionLayer>();
    }

    public NetworkedComponentType Type => NetworkedComponentType.Collision;
}