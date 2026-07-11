using Ptu.Cli.Configuration;

namespace Ptu.Cli.Tests.Fakes;

public sealed class InMemoryPresetStore : IPresetStore
{
    public PresetConfig Config { get; set; } = PtuDefaults.CreateConfig();

    public int SaveCount { get; private set; }

    public Exception? ThrowOnLoad { get; set; }

    public PresetConfig Load() => ThrowOnLoad is null ? Config : throw ThrowOnLoad;

    public void Save(PresetConfig config)
    {
        Config = config;
        SaveCount++;
    }
}
