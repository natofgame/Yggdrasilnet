namespace Yggdrasilnet.Server.Utils;

public static class ConsoleFormat {
    private static readonly bool ColorEnabled = !Console.IsOutputRedirected;

    private const string Reset = "\u001b[0m";
    private const string DimCode = "\u001b[90m";
    private const string GreenCode = "\u001b[32m";
    private const string YellowCode = "\u001b[33m";
    private const string RedCode = "\u001b[31m";
    private const string CyanCode = "\u001b[36m";
    private const string BoldCode = "\u001b[1m";

    private static string Wrap(string text, string ansiCode) => ColorEnabled ? $"{ansiCode}{text}{Reset}" : text;

    public static string Dim(string text) => Wrap(text, DimCode);
    public static string Green(string text) => Wrap(text, GreenCode);
    public static string Yellow(string text) => Wrap(text, YellowCode);
    public static string Red(string text) => Wrap(text, RedCode);
    public static string Cyan(string text) => Wrap(text, CyanCode);
    public static string Bold(string text) => Wrap(text, BoldCode);

    public static string ColorByBudget(string text, double value, double budget) {
        if (budget <= 0) {
            return text;
        }

        var ratio = value / budget;
        return ratio switch {
            >= 1.0 => Red(text),
            >= 0.75 => Yellow(text),
            _ => Green(text),
        };
    }
}
