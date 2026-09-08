using LiteNetLib;

namespace Yggdrasilnet.Server;

public interface INetEvent;

public sealed record PeerConnectedEvent(NetPeer Peer) : INetEvent;
public sealed record PeerDisconnectedEvent(NetPeer Peer, DisconnectInfo Info) : INetEvent;