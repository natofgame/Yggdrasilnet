using Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.Content.Spell;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content;

public static class ContentValidator {
    public static void Validate(IDefinition definition) {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id)) {
            throw new InvalidDataException("Content definition must have a nonempty id.");
        }

        switch (definition) {
            case SpellDefinition spell:
                ValidateSpell(spell);
                break;
            case EntityDefinition entity:
                ValidateEntity(entity);
                break;
        }
    }

    public static void Validate(
        DefinitionRegistry<EntityDefinition> entities,
        DefinitionRegistry<SpellDefinition> spells) {
        foreach (var (_, id) in entities.Entries) {
            entities.TryGet(id, out var entity);
            Validate(entity);
            foreach (var spellbook in entity.Components.OfType<SpellbookComponentDefinition>()) {
                foreach (var spellId in spellbook.Spells) {
                    if (!spells.TryGet(spellId, out _)) {
                        throw Invalid(entity.Id, $"spellbook references unknown spell '{spellId}'");
                    }
                }
            }
        }

        foreach (var (_, id) in spells.Entries) {
            spells.TryGet(id, out var spell);
            Validate(spell);
            if (!string.IsNullOrEmpty(spell.ProjectileEntityId) && !entities.TryGet(spell.ProjectileEntityId, out _)) {
                throw Invalid(spell.Id, $"projectileEntityId references unknown entity '{spell.ProjectileEntityId}'");
            }
        }
    }

    private static void ValidateSpell(SpellDefinition spell) {
        if (!Enum.IsDefined(spell.Type)) {
            throw Invalid(spell.Id, $"unknown spell type '{spell.Type}'");
        }

        Nonnegative(spell.Id, nameof(spell.AnticipationSeconds), spell.AnticipationSeconds);
        Nonnegative(spell.Id, nameof(spell.StrikeSeconds), spell.StrikeSeconds);
        Nonnegative(spell.Id, nameof(spell.ImpactSeconds), spell.ImpactSeconds);
        Nonnegative(spell.Id, nameof(spell.ReturnSeconds), spell.ReturnSeconds);
        Nonnegative(spell.Id, nameof(spell.Range), spell.Range);
        Nonnegative(spell.Id, nameof(spell.Damage), spell.Damage);

        if (spell.Effects is null) {
            throw Invalid(spell.Id, "effects must be a list");
        }

        foreach (var effect in spell.Effects) {
            if (effect is null || !Enum.IsDefined(effect.Type)) {
                throw Invalid(spell.Id, "effects contain a null effect or unknown effect type");
            }
            Nonnegative(spell.Id, "effect amount", effect.Amount);
        }

        if (spell.Type == SpellType.Projectile) {
            if (string.IsNullOrWhiteSpace(spell.ProjectileEntityId)) {
                throw Invalid(spell.Id, "projectileEntityId is required for a projectile spell");
            }
            Positive(spell.Id, nameof(spell.ProjectileSpeed), spell.ProjectileSpeed);
            Positive(spell.Id, nameof(spell.ProjectileLifetimeSeconds), spell.ProjectileLifetimeSeconds);
            Positive(spell.Id, nameof(spell.ProjectileHitRadius), spell.ProjectileHitRadius);
        }

        if (spell.Effects.Count == 0 && spell.Damage > 0f) {
            spell.Effects.Add(new SpellEffectDefinition { Type = SpellEffectType.Damage, Amount = spell.Damage });
        }
    }

    private static void ValidateEntity(EntityDefinition entity) {
        if (entity.Components is null) {
            throw Invalid(entity.Id, "components must be a list");
        }

        foreach (var component in entity.Components) {
            switch (component) {
                case null:
                    throw Invalid(entity.Id, "components must not contain null");
                case HealthComponentDefinition health:
                    Positive(entity.Id, "health max", health.Max);
                    break;
                case TargetComponentDefinition target:
                    Nonnegative(entity.Id, "target range", target.Range);
                    break;
                case SpellbookComponentDefinition spellbook:
                    if (spellbook.Spells is null || spellbook.Spells.Any(string.IsNullOrWhiteSpace)) {
                        throw Invalid(entity.Id, "spellbook must contain a list of nonempty spell ids");
                    }
                    if (spellbook.Spells.Count > byte.MaxValue + 1) {
                        throw Invalid(entity.Id, "spellbook contains more slots than a byte index can address");
                    }
                    break;
                case AutocastComponentDefinition autocast:
                    Positive(entity.Id, "autocast intervalSeconds", autocast.IntervalSeconds);
                    var spells = entity.Components.OfType<SpellbookComponentDefinition>().LastOrDefault()?.Spells;
                    if (spells is null || autocast.SpellIndex >= spells.Count) {
                        throw Invalid(entity.Id, "autocast spellIndex must reference a slot in the entity's spellbook");
                    }
                    break;
            }
        }
    }

    private static void Nonnegative(string id, string field, float value) {
        if (!float.IsFinite(value) || value < 0f) {
            throw Invalid(id, $"{field} must be finite and nonnegative");
        }
    }

    private static void Positive(string id, string field, float value) {
        if (!float.IsFinite(value) || value <= 0f) {
            throw Invalid(id, $"{field} must be finite and positive");
        }
    }

    private static InvalidDataException Invalid(string id, string message) => new($"Content '{id}': {message}.");
}
