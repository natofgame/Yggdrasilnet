using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using Yggdrasilnet.Server;
using Yggdrasilnet.Server.Services;
using Yggdrasilnet.Server.Services.Debug;
using Yggdrasilnet.Server.Simulation;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information() // les logs Debug (verbeux) restent disponibles via Log.Debug mais sont masqués par défaut
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Code)
    .CreateLogger();

const int port = 9050;
const int tickRate = 30;

var simulation = new Simulation();
var netServer = new NetServer(simulation.IncomingEvents);
netServer.Start(port);

var gameLoop = new GameLoop(netServer, simulation, tickRate);
simulation.NetServer = netServer;

var statsReporter = new DebugService(netServer, simulation, tickRate);
statsReporter.Attach(gameLoop);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) => {
    args.Cancel = true;
    cts.Cancel();
};

Log.Information("Game loop running at {TickRate} tps", tickRate);
gameLoop.Run(cts.Token);

netServer.Stop();
Log.Information("Server stopped.");
