using Yggdrasilnet.Gameplay.Enums;
using Yggdrasilnet.Network.Enums;

namespace Yggdrasilnet.Server.Utils;

public static class CollisionUtil {
    public static CollisionLayer ParseLayers(string layerName) {
        var result = CollisionLayer.None;
        if (string.IsNullOrWhiteSpace(layerName)) {
            return result;
        }

        foreach (var part in layerName.Split('|')) {
            result |= part.Trim().ToLowerInvariant() switch {
                "world" => CollisionLayer.World,
                "player" => CollisionLayer.Player,
                "monster" => CollisionLayer.Monster,
                "spell" => CollisionLayer.Spell,
                "all" => CollisionLayer.All,
                _ => CollisionLayer.None
            };
        }

        return result;
    }
}
