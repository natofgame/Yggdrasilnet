using Yggdrasilnet.Shared.Compatibility;

namespace Yggdrasilnet.Server.Utils;

public static class CollisionUtil {
    public static CollisionLayer GetString(string layerName) {
        var result = CollisionLayer.None;
        foreach (var part in layerName.Split('|')) {
            result |= part.Trim().ToLower() switch {
                "world"   => CollisionLayer.World,
                "player"  => CollisionLayer.Player,
                "monster" => CollisionLayer.Monster,
                "spell"   => CollisionLayer.Spell,
                "all"     => CollisionLayer.All,
                _         => CollisionLayer.None
            };
        }
        return result;
    }
}