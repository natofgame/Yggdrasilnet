using System.Numerics;
using Yggdrasilnet.Gameplay.Enums;
using Yggdrasilnet.Maths.Collision;
using Yggdrasilnet.Server.Simulation.Content;
using Yggdrasilnet.Server.Simulation.Content.Spell;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World.System.Physic;

public sealed class ProjectileSystem(SpellEffectApplier effects, DefinitionRegistry<SpellDefinition> spells) : ISystem {
    private const float MinDirectionSq = 0.0001f;

    private readonly List<(Entity Entity, ProjectileComponent Projectile)> _active = [];
    private readonly List<Entity> _expired = [];
    private readonly List<Entity> _hits = [];

    public void Update(World world, float dt) {
        if (!float.IsFinite(dt) || dt <= 0f) {
            return;
        }

        _active.Clear();
        foreach (var (entity, projectile) in world.Query<ProjectileComponent>()) {
            _active.Add((entity, projectile));
        }

        _expired.Clear();
        foreach (var (entity, projectile) in _active) {
            if (Step(world, entity, projectile, dt)) {
                _expired.Add(entity);
            }
        }

        foreach (var entity in _expired) {
            world.Despawn(entity.Id);
        }

        _expired.Clear();
        _active.Clear();
    }

    private bool Step(World world, Entity entity, ProjectileComponent projectile, float dt) {
        if (projectile.Context is not { } context) {
            return true;
        }

        if (!spells.TryGet(projectile.SpellDefinitionId, out var spell)) {
            return true;
        }

        if (!UpdateHoming(world, entity, projectile)) {
            return true;
        }

        projectile.ElapsedSeconds += dt;

        var from = entity.Position;
        var delta = projectile.Direction * (projectile.Speed * dt);
        entity.Position = from + delta;

        var half = new Vector3(projectile.HitRadius);
        var volume = BoundingBoxes.FromOffsets(from, -half, half).Sweep(delta);

        _hits.Clear();
        foreach (var target in world.QueryBox(volume, (e, c) =>
                     c.Layer == projectile.TargetLayer && !projectile.AlreadyHit.Contains(e.Id))) {
            _hits.Add(target);
        }

        _hits.Sort((a, b) => Vector3.DistanceSquared(a.Position, from)
            .CompareTo(Vector3.DistanceSquared(b.Position, from)));

        foreach (var target in _hits) {
            if (!SpellCastValidator.IsLiving(target)) {
                continue;
            }

            projectile.AlreadyHit.Add(target.Id);

            var hitContext = context with { Direction = FlatDirection(projectile.Direction, context.Direction) };
            var scale = MathF.Pow(projectile.ChainDamageMultiplier, projectile.BouncesDone);
            effects.ApplyToTarget(world, hitContext, target, spell, SpellPhase.Strike, scale);

            if (projectile.ChainRadius > 0f) {
                if (TryBounce(world, entity, projectile, target)) {
                    break;
                }

                return true;
            }

            if (--projectile.RemainingHits <= 0) {
                return true;
            }
        }

        return projectile.ElapsedSeconds >= projectile.MaxLifetimeSeconds;
    }

    private static bool UpdateHoming(World world, Entity entity, ProjectileComponent projectile) {
        if (projectile.HomingTarget is not { } homing) {
            return true;
        }

        if (!SpellCastValidator.IsLiving(homing)) {
            if (!TryFindChainTarget(world, projectile, entity.Position, out homing)) {
                return false;
            }

            projectile.HomingTarget = homing;
        }

        var toAim = homing.Position + Vector3.UnitY * projectile.AimHeight - entity.Position;
        if (toAim.LengthSquared() > MinDirectionSq) {
            projectile.Direction = Vector3.Normalize(toAim);
        }

        return true;
    }

    private static bool TryBounce(World world, Entity entity, ProjectileComponent projectile, Entity hitTarget) {
        if (projectile.BouncesRemaining <= 0
            || !TryFindChainTarget(world, projectile, hitTarget.Position, out var next)) {
            return false;
        }

        projectile.BouncesRemaining--;
        projectile.BouncesDone++;
        projectile.HomingTarget = next;
        projectile.ElapsedSeconds = 0f;

        entity.Position = hitTarget.Position + Vector3.UnitY * projectile.AimHeight;

        var toNext = next.Position + Vector3.UnitY * projectile.AimHeight - entity.Position;
        if (toNext.LengthSquared() > MinDirectionSq) {
            projectile.Direction = Vector3.Normalize(toNext);
        }

        return true;
    }

    private static bool TryFindChainTarget(World world, ProjectileComponent projectile, Vector3 center, out Entity next) {
        next = null!;
        var radius = projectile.ChainRadius;
        var radiusSq = radius * radius;
        var bestSq = float.MaxValue;
        var found = false;

        var half = new Vector3(radius);
        var volume = BoundingBoxes.FromOffsets(center, -half, half);

        foreach (var candidate in world.QueryBox(volume, (e, c) =>
                     c.Layer == projectile.TargetLayer && !projectile.AlreadyHit.Contains(e.Id))) {
            if (!SpellCastValidator.IsLiving(candidate)) {
                continue;
            }

            var distSq = ((candidate.Position - center) with { Y = 0f }).LengthSquared();
            if (distSq > radiusSq || distSq >= bestSq) {
                continue;
            }

            bestSq = distSq;
            next = candidate;
            found = true;
        }

        return found;
    }

    private static Vector3 FlatDirection(Vector3 direction, Vector3 fallback) {
        var flat = direction with { Y = 0f };
        return flat.LengthSquared() > MinDirectionSq ? Vector3.Normalize(flat) : fallback;
    }
}