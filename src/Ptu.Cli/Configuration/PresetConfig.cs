namespace Ptu.Cli.Configuration;

/// <summary>A named set of default regions and models.</summary>
public sealed class Preset
{
    public List<string> Regions { get; set; } = [];

    public List<string> Models { get; set; } = [];
}

/// <summary>Root of the persisted CLI configuration.</summary>
public sealed class PresetConfig
{
    /// <summary>Availability API endpoint; prompted for on first use and stored here.</summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>Session cookie ("name=value") sent to the availability API; set via 'ptu auth set'.</summary>
    public string? AuthCookie { get; set; }

    /// <summary>Name of the preset used when no --preset option is given.</summary>
    public string DefaultPreset { get; set; } = PtuDefaults.DefaultPresetName;

    public Dictionary<string, Preset> Presets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
