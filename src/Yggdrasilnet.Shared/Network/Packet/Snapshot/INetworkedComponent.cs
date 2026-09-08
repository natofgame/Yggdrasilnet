using LiteNetLib.Utils;

namespace Yggdrasilnet.Shared.Network.Packet.Snapshot;

public interface INetworkedComponent : INetSerializable {
    NetworkedComponentType Type { get; }
}