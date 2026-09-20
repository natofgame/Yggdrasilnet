using Yggdrasilnet.Gameplay.Enums;
using Yggdrasilnet.Server.Simulation.Content;
using Yggdrasilnet.Server.Simulation.Content.Spell;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System.Combat;

public sealed class SpellPhaseSystem(
    DefinitionRegistry<SpellDefinition> spellDefinitions
) : ISystem {
    private readonly SpellCastPipeline _pipeline = new(new SpellEffectApplier());
    
    public void Update(World world, float deltaTime) {
        if (!float.IsFinite(deltaTime) || deltaTime < 0f) {
            return;
        }
        
        foreach (var (entity, action) in world.Query<ActionStateComponent>().ToArray()) {
            if (action.Phase == SpellPhase.None) {
                continue;
            }
            
            if (!SpellCastValidator.IsLiving(entity) || action.Context is null ||
                !spellDefinitions.TryGet(action.SpellId, out var spell)) {
                Reset(action);
                continue;
            }

            var remaining = deltaTime;
            while (action.Phase != SpellPhase.None) {
                if (action.PhaseTimeRemaining > remaining) {
                    action.PhaseTimeRemaining -= remaining;
                    action.PhaseProgress01 = action.CurrentPhaseDuration > 0f
                        ? Math.Clamp(1f - action.PhaseTimeRemaining / action.CurrentPhaseDuration, 0f, 1f)
                        : 1f;
                    break;
                }
                remaining -= MathF.Max(0f, action.PhaseTimeRemaining);
                if (action.Phase == SpellPhase.Strike || action.Phase == SpellPhase.Anticipation) {
                    _pipeline.Cast(world, entity, spell, action.Phase);
                }
                
                action.Phase = action.Phase switch {
                    SpellPhase.Anticipation => SpellPhase.Strike,
                    SpellPhase.Strike => SpellPhase.Impact,
                    SpellPhase.Impact => SpellPhase.Return,
                    _ => SpellPhase.None
                };
                if (action.Phase == SpellPhase.None) {
                    Reset(action);
                    break;
                }

                var timing = spell.Timing;
                action.CurrentPhaseDuration = action.Phase switch {
                    SpellPhase.Strike => timing.Strike,
                    SpellPhase.Impact => timing.Impact,
                    SpellPhase.Return => timing.Return,
                    _ => 0f
                };
                action.PhaseTimeRemaining = action.CurrentPhaseDuration;
                action.PhaseProgress01 = 0f;
            }
        }
    }

    private static void Reset(ActionStateComponent action) {
        action.Phase = SpellPhase.None;
        action.ActionType = 0;
        action.PhaseProgress01 = 0f;
        action.PhaseTimeRemaining = action.CurrentPhaseDuration = 0f;
        action.Context = null;
    }
    
    public bool TryInterruptReturn(World world, ActionStateComponent action) {
        if (action.Phase != SpellPhase.Return || action.Context is null) {
            return false;
        }
        if (!spellDefinitions.TryGet(action.SpellId, out var currentSpell) || !currentSpell.SkipReturnPhase) {
            return false;
        }
        Reset(action);
        return true;
    }
}
