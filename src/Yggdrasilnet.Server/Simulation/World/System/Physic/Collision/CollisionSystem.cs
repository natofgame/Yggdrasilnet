using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Maths;

namespace Yggdrasilnet.Server.Simulation.World.System.Physic.Collision;

public class CollisionSystem : ISystem {
    public void Update(World world, float deltaTime) {
        var entities = world.Query<CollisionComponent>().ToList();
        var boxes = new BoundingBoxes[entities.Count];

        for (var i = 0; i < entities.Count; i++) {
            var (entity, collider) = entities[i];
            boxes[i] = collider.GetWorldBoundingBoxes(entity.Position);
        }

        for (var i = 0; i < entities.Count; i++) {
            var (entityA, colliderA) = entities[i];
            var dynamicA = entityA.HasComponent<VelocityComponent>();

            for (var j = i + 1; j < entities.Count; j++) {
                var (entityB, colliderB) = entities[j];

                if (!dynamicA && !entityB.HasComponent<VelocityComponent>()) {
                    continue;
                }

                if (!colliderA.CanCollide(colliderB)) {
                    continue;
                }

                if (!boxes[i].Intersects(boxes[j])) {
                    continue;
                }

                if (colliderA.IsTrigger || colliderB.IsTrigger) {
                    continue;
                }

                var positionA = entityA.Position;
                if (TryResolve(ref positionA, colliderA, entityB.Position, colliderB)) {
                    entityA.Position = positionA;
                    boxes[i] = colliderA.GetWorldBoundingBoxes(positionA);
                }
            }
        }
    }
    
    private bool TryResolve(ref Vector3 movingPosition, in CollisionComponent collider, in Vector3 otherPosition, in CollisionComponent otherCollider) {
        var a = collider.GetWorldBoundingBoxes(movingPosition);
        var b = otherCollider.GetWorldBoundingBoxes(otherPosition);

        if (!a.Intersects(b)) {
            return false;
        }
        
        var overlapX1 = b.Max.X - a.Min.X;
        var overlapX2 = a.Max.X - b.Min.X;
        var overlapY1 = b.Max.Y - a.Min.Y;
        var  overlapY2 = a.Max.Y - b.Min.Y;
        var overlapZ1 = b.Max.Z - a.Min.Z;
        var overlapZ2 = a.Max.Z - b.Min.Z;

        var pushX = (overlapX1 < overlapX2) ? overlapX1 : -overlapX2;
        var pushY = (overlapY1 < overlapY2) ? overlapY1 : -overlapY2;
        var pushZ = (overlapZ1 < overlapZ2) ? overlapZ1 : -overlapZ2;

        var absX = MathF.Abs(pushX);
        var absY = MathF.Abs(pushY);
        var absZ = MathF.Abs(pushZ);
        
        if (absX <= absY && absX <= absZ) {
            movingPosition = movingPosition with { X = movingPosition.X + pushX };
        }
        else if (absY <= absX && absY <= absZ) {
            movingPosition = movingPosition with { Y = movingPosition.Y + pushY };
        }
        else {
            movingPosition = movingPosition with { Z = movingPosition.Z + pushZ };
        }
        return true;
    }
}