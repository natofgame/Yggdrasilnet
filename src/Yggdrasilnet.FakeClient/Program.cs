using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using Yggdrasilnet.FakeClient.LoadTest;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Code)
    .CreateLogger();

var options = LoadTestOptions.Parse(args);
var runner = new LoadTestRunner(options);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => {
    eventArgs.Cancel = true;
    cts.Cancel();
};

try {
    runner.Run(cts.Token);
} finally {
    runner.Dispose();
}

Log.Information("Load test stopped.");
