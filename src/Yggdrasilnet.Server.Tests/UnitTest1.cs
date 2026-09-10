using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using Yggdrasilnet.Server.Services;
using Yggdrasilnet.Server.Simulation;

namespace Yggdrasilnet.Server.Tests;

public sealed class NetServerConnectionTests {
    [Fact]
    public void ClientConnectsWithValidConnectionKey() {
        var server = new NetServer(new ConcurrentQueue<ISimulationEvent>());
        var port = StartServerWithAvailablePort(server);
        var listener = new TestClientListener();
        var client = new NetManager(listener) { AutoRecycle = true };

        client.Start();
        client.Connect("127.0.0.1", port, "Yggdrasilnet");

        try {
            WaitForCondition(server, client, () => listener.IsConnected, TimeSpan.FromSeconds(5));
            Assert.True(listener.IsConnected);
        }
        finally {
            client.Stop();
            server.Stop();
        }
    }

    [Fact]
    public void ClientIsRejectedWithInvalidConnectionKey() {
        var server = new NetServer(new ConcurrentQueue<ISimulationEvent>());
        var port = StartServerWithAvailablePort(server);
        var listener = new TestClientListener();
        var client = new NetManager(listener) { AutoRecycle = true };

        client.Start();
        client.Connect("127.0.0.1", port, "InvalidKey");

        try {
            WaitForCondition(server, client, () => listener.WasDisconnected, TimeSpan.FromSeconds(5));
            Assert.False(listener.IsConnected);
            Assert.True(listener.WasDisconnected);
        }
        finally {
            client.Stop();
            server.Stop();
        }
    }

    private static void WaitForCondition(NetServer server, NetManager client, Func<bool> condition, TimeSpan timeout) {
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt) {
            server.Poll();
            client.PollEvents();

            if (condition()) {
                return;
            }

            Thread.Sleep(15);
        }

        throw new TimeoutException("Timed out while waiting for network condition.");
    }

    private static int StartServerWithAvailablePort(NetServer server) {
        const int attempts = 20;

        for (var i = 0; i < attempts; i++) {
            var port = GetFreeUdpPort();
            try {
                server.Start(port);
                return port;
            }
            catch (InvalidOperationException) {
                // Some ephemeral ports can be denied by OS policies on Windows.
            }
        }

        throw new InvalidOperationException("Unable to bind a UDP port for NetServer tests after multiple attempts.");
    }

    private static int GetFreeUdpPort() {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private sealed class TestClientListener : INetEventListener {
        public bool IsConnected { get; private set; }
        public bool WasDisconnected { get; private set; }

        public void OnPeerConnected(NetPeer peer) => IsConnected = true;
        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) => WasDisconnected = true;
        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
            reader.Recycle();
        }
        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
            reader.Recycle();
        }
        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
        public void OnConnectionRequest(ConnectionRequest request) { }
    }
}
