using System.Numerics;
using Yggdrasilnet.Server.Simulation.Content;
using Yggdrasilnet.Server.Simulation.Content.Spell;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System.Physic;

public sealed class ProjectileSystem(
    DefinitionRegistry<SpellDefinition> spellDefinitions,
    SpellEffectResolver effects
) : ISystem {
    public void Update(World world, float deltaTime) {
        if (!float.IsFinite(deltaTime) || deltaTime < 0f) {
            return;
        }
        foreach (var (entity, projectile) in world.Query<ProjectileComponent>().ToArray()) {
            if (projectile.Context is null || !world.TryGetEntity(projectile.TargetEntityId, out var target) ||
                !SpellCastValidator.IsLiving(target) || projectile.ElapsedSeconds >= projectile.MaxLifetimeSeconds) {
                world.Despawn(entity.Id);
                continue;
            }

            var step = MathF.Min(deltaTime, projectile.MaxLifetimeSeconds - projectile.ElapsedSeconds);
            var offset = target.Position - entity.Position;
            var distance = offset.Length();
            var direction = distance > 0.0001f ? offset / distance : Vector3.Zero;
            var travel = projectile.Speed * step;
            
            var hit = distance <= travel + projectile.HitRadius;
            entity.Position += direction * MathF.Min(travel, distance);
            if (entity.TryGetComponent<VelocityComponent>(out var velocity)) {
                velocity.X = direction.X * projectile.Speed;
                velocity.Y = direction.Y * projectile.Speed;
                velocity.Z = direction.Z * projectile.Speed;
            }
            projectile.ElapsedSeconds += step;
            if (hit && spellDefinitions.TryGet(projectile.SpellId, out var spell)) {
                effects.Apply(world, projectile.Context, spell);
            }
            if (hit || projectile.ElapsedSeconds >= projectile.MaxLifetimeSeconds) {
                world.Despawn(entity.Id);
            }
        }
    }
}
