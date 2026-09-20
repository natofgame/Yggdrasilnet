namespace Yggdrasilnet.Server.Simulation.Content.Spell.Effect;


public sealed class SpellEffectDefinition {
    public SpellEffectType Type { get; set; }
    public SpellEffectTarget Target { get; set; } = SpellEffectTarget.Target;
    public float Amount { get; set; }
    public float Duration { get; set; } = 0.15f;
    public bool ActiveOnAnticipation { get; set; } = false;
}
