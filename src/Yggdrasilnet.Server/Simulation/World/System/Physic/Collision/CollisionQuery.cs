using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Maths;

namespace Yggdrasilnet.Server.Simulation.World.System.Physic.Collision;

public static class CollisionQuery {
    public static void CollectOverlaps(
        World world,
        in BoundingBoxes query,
        List<Entity> results,
        Func<Entity, CollisionComponent, bool>? filter = null,
        bool useSweptBounds = true
    ) {
        results.Clear();
        foreach (var (entity, collider) in world.Query<CollisionComponent>()) {
            if (filter is not null && !filter(entity, collider)) {
                continue;
            }

            var bounds = useSweptBounds
                ? collider.GetSweptWorldAabb(entity.PreviousPosition, entity.Position)
                : collider.GetWorldAabb(entity.Position);
            if (query.Intersects(bounds)) {
                results.Add(entity);
            }
        }
    }
}
