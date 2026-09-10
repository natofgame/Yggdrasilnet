using System.Numerics;
using Serilog;
using Yggdrasilnet.Server.Simulation.Content;
using ContentEntity = Yggdrasilnet.Server.Simulation.Content.Entity;

namespace Yggdrasilnet.Server.Simulation.Managers;

public sealed class EntitySpawnManager(
    DefinitionRegistry<ContentEntity.EntityDefinition> entityDefinitions,
    EntityFactory entityFactory,
    World.World world
) {
    private const float CrowdAreaSize = 30f;
    private const float DefaultCrowdSpawnSpacing = 4f;
    private const int MaxEntitiesPerSpawnRequest = 20_000;

    public void SpawnEntities(string definitionId, int count) {
        count = Math.Clamp(count, 0, MaxEntitiesPerSpawnRequest);
        if (count <= 0) {
            return;
        }

        if (!entityDefinitions.TryGet(definitionId, out var definition)) {
            Log.Warning("Entity definition '{DefinitionId}' not found, cannot spawn {Count} entities", definitionId, count);
            return;
        }

        var random = new Random();
        var spawnPositions = BuildSpawnPositions(definitionId, definition, count, random);
        for (var i = 0; i < spawnPositions.Count; i++) {
            var position = spawnPositions[i];
            entityFactory.Create(definition, world, position);
        }

        Log.Information("Spawned {Count} '{DefinitionId}' entities ({Total} total entities now)",
            count, definitionId, world.Entities.Count);
    }

    private static List<Vector3> BuildSpawnPositions(string definitionId, ContentEntity.EntityDefinition definition, int count, Random random) {
        if (count <= 0) {
            return [];
        }

        if (!string.Equals(definitionId, "crowd", StringComparison.OrdinalIgnoreCase)) {
            return BuildRandomSquarePositions(count, CrowdAreaSize, random);
        }

        var spacing = ResolveCrowdSpawnSpacing(definition);
        return BuildSpacedGridPositions(count, spacing);
    }

    private static float ResolveCrowdSpawnSpacing(ContentEntity.EntityDefinition definition) {
        foreach (var component in definition.Components) {
            if (component is ContentEntity.SteeringComponentDefinition steering && steering.SpawnSpacing > 0f) {
                return steering.SpawnSpacing;
            }
        }

        return DefaultCrowdSpawnSpacing;
    }

    private static List<Vector3> BuildRandomSquarePositions(int count, float areaSize, Random random) {
        var positions = new List<Vector3>(count);
        var half = areaSize / 2f;
        for (var i = 0; i < count; i++) {
            positions.Add(new Vector3(
                (float)(random.NextDouble() * 2f - 1f) * half,
                0f,
                (float)(random.NextDouble() * 2f - 1f) * half
            ));
        }

        return positions;
    }

    private static List<Vector3> BuildSpacedGridPositions(int count, float spacing) {
        var safeSpacing = MathF.Max(0.5f, spacing);
        var side = (int)MathF.Ceiling(MathF.Sqrt(count));
        var half = ((side - 1) * safeSpacing) / 2f;
        var positions = new List<Vector3>(count);
        for (var index = 0; index < count; index++) {
            var x = index % side;
            var z = index / side;
            positions.Add(new Vector3(
                x * safeSpacing - half,
                0f,
                z * safeSpacing - half
            ));
        }

        return positions;
    }
}
