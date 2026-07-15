using Ptu.Cli.Availability;
using Ptu.Cli.Configuration;

namespace Ptu.Cli.Tests;

public class PresetCommandsTests
{
    [Fact]
    public void PresetList_ShowsDefaultPresetAsActive()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("preset", "list");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("* default", result.Output);
        Assert.Contains("swedencentral", result.Output);
        Assert.Contains("gpt-5.4", result.Output);
        Assert.Contains("az-europe", result.Output);
    }

    [Fact]
    public void PresetShow_WithoutName_ShowsActivePreset()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("preset", "show");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("default", result.Output);
        Assert.Contains("(active)", result.Output);
        Assert.Contains("swedencentral, francecentral", result.Output);
        Assert.Contains("gpt-5.4, gpt-5.4-mini, gpt-5-mini, gpt-4.1", result.Output);
        Assert.Contains("Learn tab: az-europe", result.Output);
    }

    [Fact]
    public void PresetShow_WithUnknownName_FailsWithExitCode1()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("preset", "show", "nope");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("nope", result.Output);
    }

    [Fact]
    public void PresetSet_WithNewName_CreatesPreset()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("preset", "set", "us", "--regions", "eastus", "--models", "gpt-4.1", "--tab", "az-americas");

        Assert.Equal(0, result.ExitCode);
        var preset = store.Config.Presets["us"];
        Assert.Equal(["eastus"], preset.Regions);
        Assert.Equal(["gpt-4.1"], preset.Models);
        Assert.Equal("az-americas", preset.Tab);
    }

    [Fact]
    public void PresetSet_WithNewNameAndOnlyRegions_UsesFactoryDefaultModels()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("preset", "set", "eu", "--regions", "francecentral");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(PtuDefaults.Models, store.Config.Presets["eu"].Models);
        Assert.Equal(PaygDataZoneTabs.Default, store.Config.Presets["eu"].Tab);
    }

    [Fact]
    public void PresetSet_WithExistingName_UpdatesOnlyProvidedValues()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.Presets["eu"] = new Preset { Regions = ["francecentral"], Models = ["gpt-4.1"], Tab = "az-europe" };

        var result = app.Run("preset", "set", "eu", "--models", "gpt-5-mini,gpt-5.4", "--tab", "AZ-APAC");

        Assert.Equal(0, result.ExitCode);
        var preset = store.Config.Presets["eu"];
        Assert.Equal(["francecentral"], preset.Regions);
        Assert.Equal(["gpt-5-mini", "gpt-5.4"], preset.Models);
        Assert.Equal("az-apac", preset.Tab);
    }

    [Fact]
    public void PresetSet_WithOnlyTab_UpdatesTab()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("preset", "set", "default", "--tab", "az-mea");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("az-mea", store.Config.Presets["default"].Tab);
    }

    [Fact]
    public void PresetSet_WithUnknownTab_FailsWithoutSaving()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("preset", "set", "default", "--tab", "europe");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("az-americas, az-europe, az-apac, az-mea", result.Output);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void PresetSet_WithoutValues_FailsWithExitCode1()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("preset", "set", "eu");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Nothing to set", result.Output);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void PresetUse_SwitchesActivePreset()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.Presets["eu"] = new Preset { Regions = ["francecentral"], Models = ["gpt-4.1"], Tab = "az-apac" };

        var result = app.Run("preset", "use", "eu");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("eu", store.Config.DefaultPreset);
        Assert.Contains("Learn tab: az-apac", result.Output);
    }

    [Fact]
    public void PresetUse_WithUnknownName_FailsWithExitCode1()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("preset", "use", "nope");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(PtuDefaults.DefaultPresetName, store.Config.DefaultPreset);
    }

    [Fact]
    public void PresetRemove_DeletesPreset()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.Presets["eu"] = new Preset { Regions = ["francecentral"], Models = ["gpt-4.1"] };

        var result = app.Run("preset", "remove", "eu");

        Assert.Equal(0, result.ExitCode);
        Assert.False(store.Config.Presets.ContainsKey("eu"));
    }

    [Fact]
    public void PresetRemove_ActivePreset_FallsBackToDefault()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.Presets["eu"] = new Preset { Regions = ["francecentral"], Models = ["gpt-4.1"] };
        store.Config.DefaultPreset = "eu";

        var result = app.Run("preset", "remove", "eu");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(PtuDefaults.DefaultPresetName, store.Config.DefaultPreset);
    }

    [Fact]
    public void PresetRemove_DefaultPreset_FailsWithExitCode1()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("preset", "remove", "default");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("reset", result.Output);
        Assert.True(store.Config.Presets.ContainsKey("default"));
    }

    [Fact]
    public void PresetReset_RestoresFactoryValuesAndActivatesDefault()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.Presets["default"] = new Preset { Regions = ["eastus"], Models = ["o9"], Tab = "az-americas" };
        store.Config.Presets["eu"] = new Preset { Regions = ["francecentral"], Models = ["gpt-4.1"] };
        store.Config.DefaultPreset = "eu";

        var result = app.Run("preset", "reset");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(PtuDefaults.DefaultPresetName, store.Config.DefaultPreset);
        Assert.Equal(PtuDefaults.Regions, store.Config.Presets["default"].Regions);
        Assert.Equal(PtuDefaults.Models, store.Config.Presets["default"].Models);
        Assert.Equal(PaygDataZoneTabs.Default, store.Config.Presets["default"].Tab);
        Assert.Contains("Learn tab: az-europe", result.Output);
        Assert.True(store.Config.Presets.ContainsKey("eu"));
    }

    [Fact]
    public void PresetReset_All_RemovesOtherPresets()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.Presets["eu"] = new Preset { Regions = ["francecentral"], Models = ["gpt-4.1"] };

        var result = app.Run("preset", "reset", "--all");

        Assert.Equal(0, result.ExitCode);
        Assert.Single(store.Config.Presets);
        Assert.True(store.Config.Presets.ContainsKey("default"));
    }
}
