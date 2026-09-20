using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.Content.Ai;

public sealed class AiGroupCoordinator {
    public int MaxAttackersPerTarget { get; set; } = 2;
    public float TokenRange { get; set; } = 10f;
    public float TokenLease { get; set; } = 4f;
    public float MinGap { get; set; } = 0.4f;
    public float MaxGap { get; set; } = 1.2f;

    private readonly record struct Member(World.Entity Entity, AiComponent Ai);

    private readonly Dictionary<int, List<Member>> _groups = new();
    private readonly Dictionary<int, float> _gapTimers = new();
    private readonly List<int> _staleKeys = new();
    private readonly List<(float Rel, AiComponent Ai)> _sorted = new();

    public void Update(World.World world, float dt) {
        foreach (var list in _groups.Values) {
            list.Clear();
        }

        foreach (var (entity, ai) in world.Query<AiComponent>()) {
            if (ai.TargetEntityId is not { } targetId) {
                ReleaseToken(ai);
                ai.AlliesNearby = 0;
                continue;
            }

            if (!_groups.TryGetValue(targetId, out var list)) {
                list = new List<Member>();
                _groups[targetId] = list;
            }
            list.Add(new Member(entity, ai));
        }

        _staleKeys.Clear();
        foreach (var (targetId, members) in _groups) {
            if (members.Count == 0) {
                _staleKeys.Add(targetId);
                continue;
            }
            UpdateGroup(targetId, members, dt);
        }

        foreach (var key in _staleKeys) {
            _groups.Remove(key);
            _gapTimers.Remove(key);
        }
    }

    private void UpdateGroup(int targetId, List<Member> members, float dt) {
        _gapTimers.TryGetValue(targetId, out var gap);
        gap = MathF.Max(0f, gap - dt);

        AssignSlots(members);

        var maxAttackers = Math.Clamp((members.Count + 1) / 2, 1, MaxAttackersPerTarget);
        var holders = 0;

        foreach (var m in members) {
            var ai = m.Ai;
            ai.AlliesNearby = members.Count - 1;

            if (ai.HasAttackToken) {
                ai.TokenTimer -= dt;
                if (ai.AttackCooldown > 0f
                    || ai.TokenTimer <= 0f
                    || ai.TargetDistance > TokenRange * 1.5f) {
                    ReleaseToken(ai);
                } else {
                    holders++;
                }
            } else if (ai.AttackCooldown <= 0f) {
                ai.TokenWait += dt;
            }
        }

        if (holders < maxAttackers && gap <= 0f) {
            AiComponent? bestAi = null;
            var bestScore = float.MinValue;

            foreach (var m in members) {
                var ai = m.Ai;
                if (ai.HasAttackToken
                    || ai.AttackCooldown > 0f
                    || ai.SurprisedTimer > 0f
                    || ai.TargetDistance > TokenRange) {
                    continue;
                }

                var score = ai.TokenWait
                            + ai.Aggressivity * 0.3f
                            + ai.Impulsivity * 0.1f
                            - ai.TargetDistance * 0.1f;
                if (score > bestScore) {
                    bestScore = score;
                    bestAi = ai;
                }
            }

            if (bestAi is not null) {
                bestAi.HasAttackToken = true;
                bestAi.TokenTimer = TokenLease;
                bestAi.TokenWait = 0f;
                gap = MinGap + (float)Random.Shared.NextDouble() * (MaxGap - MinGap);
            }
        }

        _gapTimers[targetId] = gap;
    }

    private void AssignSlots(List<Member> members) {
        var anchor = members[0];
        foreach (var m in members) {
            if (m.Entity.Id < anchor.Entity.Id) {
                anchor = m;
            }
        }

        var anchorAngle = AngleFromTarget(anchor.Ai);

        _sorted.Clear();
        foreach (var m in members) {
            var rel = (AngleFromTarget(m.Ai) - anchorAngle) % MathF.Tau;
            if (rel < 0f) {
                rel += MathF.Tau;
            }
            _sorted.Add((rel, m.Ai));
        }
        _sorted.Sort((a, b) => a.Rel.CompareTo(b.Rel));

        var step = MathF.Tau / _sorted.Count;
        for (var i = 0; i < _sorted.Count; i++) {
            _sorted[i].Ai.SlotAngle = anchorAngle + i * step;
        }
    }

    private static float AngleFromTarget(AiComponent ai) =>
        MathF.Atan2(-ai.TargetDirection.Z, -ai.TargetDirection.X);

    private static void ReleaseToken(AiComponent ai) {
        ai.HasAttackToken = false;
        ai.TokenTimer = 0f;
        ai.TokenWait = 0f;
    }
}
