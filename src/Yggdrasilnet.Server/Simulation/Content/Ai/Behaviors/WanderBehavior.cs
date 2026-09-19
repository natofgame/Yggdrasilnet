using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai.Behaviors;

public sealed class WanderBehavior : AiBehavior {
    public float Speed { get; set; } = 2f;
    public float Radius { get; set; } = 6f;
    public float ArriveDistance { get; set; } = 0.5f;
    public float IdleDuration { get; set; } = 2f;

    public override float Score(AiComponent ai) => ai.HasTarget ? 0.01f : 1f;

    public override void Tick(World.Entity entity, AiComponent ai, SteeringComponent steering, float dt) {
        if (ai.WanderTarget is not { } target) {
            ai.WanderTarget = PickRandomPoint(entity.Position, Radius);
            return;
        }

        var toTarget = target - entity.Position;
        toTarget.Y = 0;
        var distance = toTarget.Length();

        if (distance <= ArriveDistance) {
            ai.WanderIdleTimer += dt;
            if (ai.WanderIdleTimer < IdleDuration) {
                return;
            }

            ai.WanderTarget = null;
            ai.WanderIdleTimer = 0f;
            return;
        }

        var dir = toTarget / distance;
        steering.InputDirection = new Vector2(dir.X, dir.Z);
        steering.MoveSpeed = Speed;
    }

    private static Vector3 PickRandomPoint(Vector3 origin, float radius) {
        var angle = (float)(Random.Shared.NextDouble() * Math.Tau);
        var dist = (float)Random.Shared.NextDouble() * radius;
        return origin + new Vector3(MathF.Cos(angle) * dist, 0f, MathF.Sin(angle) * dist);
    }
}
