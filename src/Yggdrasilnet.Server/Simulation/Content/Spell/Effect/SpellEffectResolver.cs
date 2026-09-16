using System.Numerics;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Compatibility;
using Yggdrasilnet.Shared.Maths;

namespace Yggdrasilnet.Server.Simulation.Content.Spell.Effect;

public sealed class SpellEffectResolver {
    private readonly Dictionary<SpellEffectType, Action<HealthComponent, float>> _handlers = new() {
        [SpellEffectType.Damage] = (health, amount) => health.Current = MathF.Max(0f, health.Current - amount),
        [SpellEffectType.Heal] = (health, amount) => {
            health.Current = MathF.Min(health.Max, health.Current + amount);
        }
    };

    public void Apply(World.World world, SpellCastContext context, SpellDefinition spell) {
        var origin = context.Origin + context.Direction * spell.Range;
        var spellBox = new BoundingBoxes(
            origin - spell.HitBoxMin,
            origin + spell.HitBoxMax
        );
        
        if (!world.TryGetEntity(context.CasterEntityId, out var caster)) {
            return;
        }
        
        var hits = world.QueryBox(spellBox, (entity, collider) =>
            collider.Layer == CollisionLayer.Monster && entity != caster
        );

        foreach (var target in hits) {
            foreach (var effect in spell.Effects) {
                if (effect.Type == SpellEffectType.Dash) {
                    ApplyDash(world, context, effect.Amount, effect.Duration);
                    return;
                }

                ApplyEffect(world, target, effect.Type, effect.Amount);
            }

            ApplyPunch(target, context, spell);

            if (spell.Effects.Count == 0 && spell.Damage > 0f) {
                ApplyDamage(world, target, spell.Damage);
            }
        }
    }

    public void ApplyDash(World.World world, SpellCastContext context, float speed, float duration) {
        if (!world.TryGetEntity(context.CasterEntityId, out var caster)) {
            return;
        }

        if (!caster.TryGetComponent<SteeringComponent>(out var steering))
            return;

        var dir = steering.InputDirection;

        if (dir.LengthSquared() > 0.0001f)
            dir = Vector2.Normalize(dir);
        else
            dir = Vector2.UnitX;

        steering.HasDash = true;
        steering.DashDirection = dir;
        steering.DashSpeed = speed;
        steering.DashTimer = duration;
    }

    public void ApplyPunch(World.Entity target, SpellCastContext context, SpellDefinition spell) {
        if (!target.TryGetComponent<SteeringComponent>(out var steering)) {
            return;
        }

        var direction = new Vector2(context.Direction.X, context.Direction.Z);
        if (direction.LengthSquared() <= 0.0001f) {
            return;
        }

        direction = Vector2.Normalize(direction);
        steering.HasPunch = true;
        steering.PunchDirection = direction;
        steering.PunchOverrideSpeed = 20f;
        steering.PunchTimer = 0.06f;
        steering.PunchFreezeTimer = 0.3f;
    }


    public void ApplyDamage(World.World world, World.Entity target, float damage) =>
        ApplyEffect(world, target, SpellEffectType.Damage, damage);

    public void ApplyEffect(World.World world, World.Entity target, SpellEffectType type, float amount) {
        if (!float.IsFinite(amount) || amount < 0f ||
            !SpellCastValidator.IsLiving(target) || !target.TryGetComponent<HealthComponent>(out var health)) {
            return;
        }
        
        if (!_handlers.TryGetValue(type, out var handler)) {
            throw new NotSupportedException($"Unsupported spell effect: {type}");
        }
        handler(health, amount);
    }

}
