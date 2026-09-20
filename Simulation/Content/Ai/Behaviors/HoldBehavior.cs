using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai.Behaviors;

public sealed class HoldBehavior : AiBehavior {
    public float Speed { get; set; } = 4f;
    public float BaseRadius { get; set; } = 3.5f;
    public float PrudenceRadius { get; set; } = 3f;
    public float ArriveTolerance { get; set; } = 0.6f;

    public override float Score(AiComponent ai) {
        if (!ai.HasTarget || ai.HasAttackToken) {
            return 0f;
        }

        return 0.15f;
    }

    public override void Tick(World.World world, World.Entity entity, AiComponent ai, SteeringComponent steering, float dt) {
        var radius = BaseRadius + Math.Clamp(ai.Prudence / 10f, 0f, 1f) * PrudenceRadius;

        var targetPos = entity.Position + ai.TargetDirection * ai.TargetDistance;
        var slot = targetPos + new Vector3(MathF.Cos(ai.SlotAngle), 0f, MathF.Sin(ai.SlotAngle)) * radius;

        var toSlot = slot - entity.Position;
        toSlot.Y = 0f;
        var dist = toSlot.Length();

        if (dist <= ArriveTolerance) {
            steering.MoveSpeed = 0f;
            return;
        }

        var dir = toSlot / dist;
        steering.InputDirection = new Vector2(dir.X, dir.Z);
        steering.MoveSpeed = Speed * Math.Clamp(dist / 2f, 0.3f, 1f);
    }
}
