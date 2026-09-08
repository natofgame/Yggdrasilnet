using LiteNetLib;

namespace Yggdrasilnet.Server.Simulation.Session;

public sealed class PlayerSessionRegistry {
    private readonly Dictionary<NetPeer, PlayerSession> _sessions = new();

    public IReadOnlyCollection<PlayerSession> All => _sessions.Values;

    public PlayerSession Create(NetPeer peer, long tick) {
        var session = new PlayerSession(peer, tick);
        _sessions[peer] = session;
        return session;
    }

    public bool Remove(NetPeer peer, out PlayerSession? session) {
        return _sessions.Remove(peer, out session);
    }

    public bool TryGet(NetPeer peer, out PlayerSession? session) {
        return _sessions.TryGetValue(peer, out session);
    }
}
