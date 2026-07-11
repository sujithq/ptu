namespace Ptu.Cli.Configuration;

public interface IPresetStore
{
    /// <summary>Loads the configuration, falling back to factory defaults when none exists.</summary>
    PresetConfig Load();

    void Save(PresetConfig config);
}
