using System.Numerics;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Simulation.Content.Spell;

public sealed record SpellCastContext(
    int CasterEntityId,
    string SpellId,
    SpellType SpellType,
    Vector3 Direction,
    Vector3 Origin);
