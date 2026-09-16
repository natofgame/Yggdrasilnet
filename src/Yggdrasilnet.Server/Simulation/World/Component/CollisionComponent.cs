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

    public BoundingBoxes GetWorldBoundingBoxes(Vector3 position) {
        return BoundingBoxes.From(position, Size);
    }
}