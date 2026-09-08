using LiteNetLib;
using Serilog;
using Yggdrasilnet.Shared.Network.Packet;
using Yggdrasilnet.Shared.Network.Packet.Packets;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

const string host = "127.0.0.1";
const int port = 9050;
const string connectionKey = "Yggdrasilnet";

var listener = new Yggdrasilnet.FakeClient.FakeClientListener();
var client = new NetManager(listener) { AutoRecycle = true };
var packetRegistry = new PacketRegistry();

client.Start();
client.Connect(host, port, connectionKey);

Log.Information("Connecting to {Host}:{Port} ... (Ctrl+C to quit)", host, port);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) => {
    args.Cancel = true;
    cts.Cancel();
};

var sentHello = false;

while (!cts.IsCancellationRequested) {
    client.PollEvents();

    if (!sentHello && listener.Server is { } peer) {
        var writer = new LiteNetLib.Utils.NetDataWriter();
        packetRegistry.Write(writer, new PlayerConnexionPacket { PlayerId = peer.Id, IsOwner = true });
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
        Log.Information("Sent PlayerConnexionPacket (playerId={PlayerId})", peer.Id);
        sentHello = true;
    }

    Thread.Sleep(15);
}

client.Stop();
Log.Information("Fake client stopped.");
