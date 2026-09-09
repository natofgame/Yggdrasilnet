using System.Globalization;

namespace Yggdrasilnet.FakeClient.LoadTest;

public sealed class LoadTestOptions {
    public string Host { get; private set; } = "127.0.0.1";
    public int Port { get; private set; } = 9050;

    public int StartClients { get; private set; } = 1;
    public int ClientStep { get; private set; } = 2;
    public int MaxClients { get; private set; } = 200;

    public int StartEntities { get; private set; }
    public int EntityStep { get; private set; } = 200;
    public int MaxEntities { get; private set; } = 20_000;

    public double StepIntervalSeconds { get; private set; } = 5;

    public double LatencyThresholdMs { get; private set; } = 150;
    public double TickBudgetMultiplier { get; private set; } = 1.5;

    public static LoadTestOptions Parse(string[] args) {
        var options = new LoadTestOptions();

        for (var i = 0; i < args.Length - 1; i++) {
            var value = args[i + 1];
            switch (args[i]) {
                case "--host": options.Host = value; break;
                case "--port": options.Port = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--start-clients": options.StartClients = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--client-step": options.ClientStep = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--max-clients": options.MaxClients = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--start-entities": options.StartEntities = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--entity-step": options.EntityStep = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--max-entities": options.MaxEntities = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--step-interval": options.StepIntervalSeconds = double.Parse(value, CultureInfo.InvariantCulture); break;
                case "--latency-threshold-ms": options.LatencyThresholdMs = double.Parse(value, CultureInfo.InvariantCulture); break;
                case "--tick-budget-multiplier": options.TickBudgetMultiplier = double.Parse(value, CultureInfo.InvariantCulture); break;
            }
        }

        return options;
    }
}
