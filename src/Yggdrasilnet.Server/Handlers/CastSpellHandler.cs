using System.Numerics;
using LiteNetLib;
using Yggdrasilnet.Server.Simulation;
using Yggdrasilnet.Server.Simulation.Content;
using Yggdrasilnet.Server.Simulation.Content.Spell;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Shared.Maths;
using Yggdrasilnet.Shared.Network;
using Yggdrasilnet.Shared.Network.Packet.Packets;

namespace Yggdrasilnet.Server.Handlers;

public sealed class CastSpellHandler : IPacketHandler<CastSpellPacket, ISimulationContext> {
    private readonly DefinitionRegistry<SpellDefinition> _spellDefinitions;

    public CastSpellHandler(DefinitionRegistry<SpellDefinition> spellDefinitions) {
        _spellDefinitions = spellDefinitions;
    }
    
    public void Handle(NetPeer peer, CastSpellPacket packet, ISimulationContext context) {
        if (!context.Sessions.TryGet(peer, out var session) || session!.EntityId == -1) {
            return;
        }
        
        if (!context.World.TryGetEntity(session.EntityId, out var entity)) {
            return;
        }
        
        if (entity.TryGetComponent<ActionStateComponent>(out var action) && action.Phase != Shared.Spell.SpellPhase.None) {
            return;
        }
        
        if (!entity.TryGetComponent<SpellbookComponent>(out var book) || packet.SpellIndex >= book.Spells.Count) {
            return;
        }

        var world = context.World;
 
        var spellId = book.Spells[packet.SpellIndex];
        if (!_spellDefinitions.TryGet(spellId, out var spell) ||
            !SpellCastValidator.TryCreate(context.World, entity, spell, out _)) {
            return;
        }

        entity.AddComponent(new CastSpellIntentComponent {
            SpellDefinitionId = spellId,
            SpellIndex = packet.SpellIndex
        });
    }
}