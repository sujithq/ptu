namespace Ptu.Cli.Configuration;

/// <summary>Factory defaults for regions, models, and the built-in preset.</summary>
public static class PtuDefaults
{
    public const string DefaultPresetName = "default";

    public static readonly IReadOnlyList<string> Regions = ["swedencentral", "francecentral"];

    public static readonly IReadOnlyList<string> Models = ["gpt-5.4", "gpt-5.4-mini", "gpt-5-mini", "gpt-4.1"];

    public static Preset CreateDefaultPreset() => new()
    {
        Regions = [.. Regions],
        Models = [.. Models],
    };

    public static PresetConfig CreateConfig() => new()
    {
        DefaultPreset = DefaultPresetName,
        Presets = new Dictionary<string, Preset>(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultPresetName] = CreateDefaultPreset(),
        },
    };
}
