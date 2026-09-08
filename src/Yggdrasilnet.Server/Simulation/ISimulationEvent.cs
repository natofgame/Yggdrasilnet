using LiteNetLib;
using Yggdrasilnet.Shared.Network.Packet;

namespace Yggdrasilnet.Server.Simulation;

public interface ISimulationEvent;

public sealed record PeerConnectedEvent(NetPeer Peer) : ISimulationEvent;
public sealed record PeerDisconnectedEvent(NetPeer Peer, DisconnectInfo Info) : ISimulationEvent;
public sealed record PacketReceivedEvent(NetPeer Peer, IPacket Packet) : ISimulationEvent;
