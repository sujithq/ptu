using Ptu.Cli.Configuration;

namespace Ptu.Cli.Tests;

public sealed class FilePresetStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ptu-tests", Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public FilePresetStoreTests()
    {
        _path = Path.Combine(_directory, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenFileIsMissing_ReturnsFactoryDefaults()
    {
        var store = new FilePresetStore(_path);

        var config = store.Load();

        Assert.Equal(PtuDefaults.DefaultPresetName, config.DefaultPreset);
        Assert.Equal(PtuDefaults.Regions, config.Presets[PtuDefaults.DefaultPresetName].Regions);
        Assert.Equal(PtuDefaults.Models, config.Presets[PtuDefaults.DefaultPresetName].Models);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsConfiguration()
    {
        var store = new FilePresetStore(_path);
        var config = PtuDefaults.CreateConfig();
        config.ApiEndpoint = "https://example.test/api";
        config.Presets["eu"] = new Preset { Regions = ["francecentral"], Models = ["gpt-4.1"] };
        config.DefaultPreset = "eu";

        store.Save(config);
        var loaded = new FilePresetStore(_path).Load();

        Assert.Equal("https://example.test/api", loaded.ApiEndpoint);
        Assert.Equal("eu", loaded.DefaultPreset);
        Assert.Equal(["francecentral"], loaded.Presets["eu"].Regions);
        Assert.Equal(["gpt-4.1"], loaded.Presets["eu"].Models);
        Assert.True(loaded.Presets.ContainsKey(PtuDefaults.DefaultPresetName));
    }

    [Fact]
    public void Load_PresetLookupIsCaseInsensitive()
    {
        var store = new FilePresetStore(_path);
        var config = PtuDefaults.CreateConfig();
        config.Presets["EU"] = new Preset { Regions = ["francecentral"], Models = ["gpt-4.1"] };

        store.Save(config);
        var loaded = new FilePresetStore(_path).Load();

        Assert.True(loaded.Presets.TryGetValue("eu", out _));
    }

    [Fact]
    public void Load_WhenFileIsCorrupt_ThrowsWithActionableMessage()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ not json");
        var store = new FilePresetStore(_path);

        var ex = Assert.Throws<InvalidOperationException>(store.Load);

        Assert.Contains("ptu preset reset", ex.Message);
    }
}
