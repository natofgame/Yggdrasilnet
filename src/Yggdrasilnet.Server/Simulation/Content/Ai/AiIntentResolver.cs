using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Utils;

namespace Yggdrasilnet.Server.Simulation.Content.Ai;

public static class AiIntentResolver {
    public static AiIntentType Resolve(World.World world, AiContext context) {
        if (!world.TryGetEntity(context.Id, out var entity)) {
            return AiIntentType.None;
        }
        if (!entity.TryGetComponent<AiComponent>(out var ai)) {
            return AiIntentType.None;
        }
        if (context.HealthRatio <= 0f) {
            return AiIntentType.None;
        }
        if (!context.HasTarget) {
            return AiIntentType.None;
        }

        // un cast en cours ne se réévalue pas : AttackIntentHandler gère déjà le freeze
        if (context.IsCasting) {
            return AiIntentType.Attack;
        }

        float bestScore = 0f;
        var bestIntent = AiIntentType.None;

        {
            var score = 0f;

            if (!context.RecentlyAttacked)
            {
                var baseAggro = AIUtility.Multiply(
                    context.HealthRatio,
                    ai.Aggressivity / 10f
                );

                // Bonus si la cible est blessée
                var targetWeakness =
                    (1f - context.TargetHealthRatio) * 0.25f;

                // Une IA impulsive devient plus agressive lorsqu'elle est blessée
                var recklessBonus =
                    (ai.Impulsivity / 10f)
                    * (1f - context.HealthRatio)
                    * 0.25f;

                score = baseAggro + targetWeakness + recklessBonus;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIntent = AiIntentType.Attack;
            }
        }
        // Flee
        {
            float score = AIUtility.Multiply(
                (ai.Prudence / 10f),
                (1f - ai.Courage / 10f)
            );
            if (score > bestScore) {
                bestScore = score;
                bestIntent = AiIntentType.Flee;
            }
        }

        // Flank
        {
            float score = AIUtility.Multiply(
                ai.Curiosity / 10f,
                AIUtility.Clamp01(context.TargetDistance / 10f)
            );
            if (score > bestScore) {
                bestScore = score;
                bestIntent = AiIntentType.Flank;
            }
        }

        return bestIntent;
    }
}