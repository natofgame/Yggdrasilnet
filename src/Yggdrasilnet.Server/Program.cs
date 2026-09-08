using Serilog;
using Yggdrasilnet.Server;
using Yggdrasilnet.Server.Simulation;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

const int port = 9050;
const int tickRate = 30;

// Simulation runs the fixed-tick game logic; NetServer only polls the socket and feeds
// simulation.IncomingEvents. The two never call into each other directly.
var simulation = new Simulation();
var netServer = new NetServer(simulation.IncomingEvents);
netServer.Start(port);

var gameLoop = new GameLoop(netServer, simulation, tickRate);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) => {
    args.Cancel = true;
    cts.Cancel();
};

Log.Information("Game loop running at {TickRate} tps", tickRate);
gameLoop.Run(cts.Token);

netServer.Stop();
Log.Information("Server stopped.");
