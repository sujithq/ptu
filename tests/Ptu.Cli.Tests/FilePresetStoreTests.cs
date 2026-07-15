using Ptu.Cli.Availability;
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
        Assert.Equal(PaygDataZoneTabs.Default, config.Presets[PtuDefaults.DefaultPresetName].Tab);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsConfiguration()
    {
        var store = new FilePresetStore(_path);
        var config = PtuDefaults.CreateConfig();
        config.ApiEndpoint = "https://example.test/api";
        config.Presets["us"] = new Preset { Regions = ["eastus"], Models = ["gpt-4.1"], Tab = "az-americas" };
        config.DefaultPreset = "us";

        store.Save(config);
        var loaded = new FilePresetStore(_path).Load();

        Assert.Equal("https://example.test/api", loaded.ApiEndpoint);
        Assert.Equal("us", loaded.DefaultPreset);
        Assert.Equal(["eastus"], loaded.Presets["us"].Regions);
        Assert.Equal(["gpt-4.1"], loaded.Presets["us"].Models);
        Assert.Equal("az-americas", loaded.Presets["us"].Tab);
        Assert.True(loaded.Presets.ContainsKey(PtuDefaults.DefaultPresetName));
    }

    [Fact]
    public void Load_LegacyPresetWithoutTab_DefaultsToEurope()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, """
                        {
                            "defaultPreset": "legacy",
                            "presets": {
                                "legacy": {
                                    "regions": ["francecentral"],
                                    "models": ["gpt-4.1"]
                                }
                            }
                        }
                        """);

        var loaded = new FilePresetStore(_path).Load();

        Assert.Equal(PaygDataZoneTabs.Default, loaded.Presets["legacy"].Tab);
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
