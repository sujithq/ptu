using System.Text.Json;

namespace Ptu.Cli.Configuration;

/// <summary>Persists presets as JSON under the user's application-data folder.</summary>
public sealed class FilePresetStore(string path) : IPresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public FilePresetStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ptu",
            "config.json"))
    {
    }

    public PresetConfig Load()
    {
        if (!File.Exists(path))
        {
            return PtuDefaults.CreateConfig();
        }

        PresetConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<PresetConfig>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Configuration file '{path}' is not valid JSON: {ex.Message}. Fix it or run 'ptu preset reset'.", ex);
        }

        if (config is null)
        {
            throw new InvalidOperationException(
                $"Configuration file '{path}' is empty. Fix it or run 'ptu preset reset'.");
        }

        // Re-wrap so preset lookups stay case-insensitive after deserialization.
        config.Presets = new Dictionary<string, Preset>(config.Presets, StringComparer.OrdinalIgnoreCase);
        return config;
    }

    public void Save(PresetConfig config)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
    }
}
