using Serilog;
using Yggdrasilnet.Server;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

const int port = 9050;

var server = new NetServer(tickRate: 30);
server.Start(port);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) => {
    args.Cancel = true;
    cts.Cancel();
};

await server.RunAsync(cts.Token);

server.Stop();
Log.Information("Server stopped.");
