namespace Yggdrasilnet.Server.Simulation.World.System.Steering;

internal static class SteeringConstants {
    public const int MaxNeighborsPerEntity = 10;
    public const float NearPlayerDistance = 18f;
    public const float MidPlayerDistance = 36f;
    public const float WanderAvoidDistance = 28f;
    public const float PlayerGridCellSize = MidPlayerDistance;
    public const float MinDistanceSquared = 0.0001f;

    public const float NearPlayerDistanceSquared = NearPlayerDistance * NearPlayerDistance;
    public const float MidPlayerDistanceSquared = MidPlayerDistance * MidPlayerDistance;
    public const float WanderAvoidDistanceSquared = WanderAvoidDistance * WanderAvoidDistance;
}
