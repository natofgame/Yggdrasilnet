using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.System.Physic.Collision;
using Yggdrasilnet.Shared.Maths;
using Yggdrasilnet.Shared.Network.Packet.Snapshot.Components;

namespace Yggdrasilnet.Server.Simulation.World.Component;

public class CollisionComponent : Shared.Network.Packet.Snapshot.Components.CollisionComponent, IComponent {

    public bool CanCollide(CollisionComponent other) {
        var aToB = (Mask & other.Layer) != 0;
        var bToA = (other.Mask & Layer) != 0;
        return aToB && bToA;
    }

    public BoundingBoxes GetWorldAabb(Vector3 position) {
        return BoundingBoxes.From(position, Size);
    }

    public BoundingBoxes GetSweptWorldAabb(Vector3 from, Vector3 to) {
        var start = GetWorldAabb(from);
        if (from == to) {
            return start;
        }

        return start.Encapsulate(GetWorldAabb(to));
    }
}