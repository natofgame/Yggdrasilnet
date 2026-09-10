using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Simulation.World.System.Steering.Behaviors;

namespace Yggdrasilnet.Server.Simulation.World.System.Steering;

public sealed class SteeringSystem : ISystem {
    private static readonly ISteeringBehavior[] Behaviors = [
        new SeekSteeringBehavior(),
        new CircleSteeringBehavior(),
        new TargetAvoidSteeringBehavior(),
        new NeighborAvoidanceSteeringBehavior(),
        new WanderSteeringBehavior(),
        new RoamConstraintSteeringBehavior()
    ];

    private readonly SteeringContext _context = new();

    public void Update(World world, float deltaTime) {
        _context.Tick++;
        _context.DeltaTime = deltaTime;
        _context.ResetForTick();

        var maxAvoidRadius = 0f;
        var hasAvoidance = false;
        foreach (var (entity, steering) in world.Query<SteeringComponent>()) {
            if (!entity.TryGetComponent<VelocityComponent>(out var velocity)) {
                continue;
            }

            _context.SteeredAgents.Add(new SteeringAgent(entity, steering, velocity, Flat(entity.Position)));
            hasAvoidance |= HasAvoidance(steering);
            if (steering.AvoidRadius > maxAvoidRadius) {
                maxAvoidRadius = steering.AvoidRadius;
            }
        }

        if (_context.SteeredAgents.Count == 0) {
            _context.FinishGridBuild();
            return;
        }

        var cellSize = MathF.Max(0.1f, maxAvoidRadius);
        _context.CellSize = cellSize;
        if (hasAvoidance) {
            _context.BuildSteeringGrid(cellSize);
        }

        foreach (var (entity, _) in world.Query<InputComponent>()) {
            _context.PlayerTargets.Add(new PlayerTarget(entity, Flat(entity.Position)));
        }

        if (_context.PlayerTargets.Count == 0) {
            _context.FinishGridBuild();
            ApplyNoPlayerSteering();
            return;
        }

        _context.BuildPlayerGrid(SteeringConstants.PlayerGridCellSize);
        _context.FinishGridBuild();

        var steeredAgents = _context.SteeredAgents;
        for (var i = 0; i < steeredAgents.Count; i++) {
            var agent = steeredAgents[i];
            if (!_context.TryFindNearestTarget(agent.Position, out var target, out var nearestPlayerDistanceSquared)) {
                continue;
            }

            var updateInterval = ResolveUpdateInterval(nearestPlayerDistanceSquared);
            if (updateInterval > 1 && ((_context.Tick + agent.Entity.Id) % updateInterval) != 0) {
                continue;
            }

            _context.EnableWanderAndAvoid = nearestPlayerDistanceSquared <= SteeringConstants.WanderAvoidDistanceSquared;
            PrepareDirection(agent.Position, target.Position);

            RunBehaviors(agent);
        }
    }

    private void ApplyNoPlayerSteering() {
        _context.EnableWanderAndAvoid = true;
        _context.HasDirection = false;

        var steeredAgents = _context.SteeredAgents;
        for (var i = 0; i < steeredAgents.Count; i++) {
            RunBehaviors(steeredAgents[i]);
        }
    }

    private void RunBehaviors(SteeringAgent agent) {
        var steer = Vector2.Zero;
        for (var i = 0; i < Behaviors.Length; i++) {
            Behaviors[i].Apply(_context, agent, ref steer);
        }

        ApplySteeringVelocity(agent, steer);
    }

    private void PrepareDirection(Vector2 agentPosition, Vector2 targetPosition) {
        var toTarget = targetPosition - agentPosition;
        var distanceSquared = toTarget.LengthSquared();
        if (distanceSquared <= SteeringConstants.MinDistanceSquared) {
            _context.HasDirection = false;
            return;
        }

        var inverseDistance = 1f / MathF.Sqrt(distanceSquared);
        _context.HasDirection = true;
        _context.Direction = toTarget * inverseDistance;
        _context.Distance = distanceSquared * inverseDistance;
    }

    private static int ResolveUpdateInterval(float nearestPlayerDistanceSquared) {
        if (nearestPlayerDistanceSquared <= SteeringConstants.NearPlayerDistanceSquared) {
            return 1;
        }

        if (nearestPlayerDistanceSquared <= SteeringConstants.MidPlayerDistanceSquared) {
            return 2;
        }

        return 3;
    }

    private static void ApplySteeringVelocity(SteeringAgent agent, Vector2 steer) {
        var steerLengthSquared = steer.LengthSquared();
        if (steerLengthSquared <= SteeringConstants.MinDistanceSquared) {
            agent.Velocity.X = 0f;
            agent.Velocity.Z = 0f;
            return;
        }

        var steerLength = MathF.Sqrt(steerLengthSquared);
        var direction = steer / steerLength;
        var speedFactor = MathF.Min(1f, steerLength);
        var speed = agent.Steering.MoveSpeed * speedFactor;

        agent.Velocity.X = direction.X * speed;
        agent.Velocity.Z = direction.Y * speed;
    }

    private static bool HasAvoidance(SteeringComponent steering) =>
        steering.AvoidRadius > 0f && steering.AvoidWeight != 0f;

    private static Vector2 Flat(Vector3 position) => new(position.X, position.Z);
}
