namespace Ptu.Cli.Commands;

internal static class CommandInput
{
    /// <summary>Flattens repeatable options, splits comma-separated entries, trims, and de-duplicates.</summary>
    public static List<string> Normalize(IEnumerable<string> values) =>
        [.. values
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>True when the value is an absolute http or https URL.</summary>
    public static bool IsValidHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
