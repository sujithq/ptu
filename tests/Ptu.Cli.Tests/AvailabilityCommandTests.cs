using Ptu.Cli.Configuration;
using Ptu.Cli.Tests.Fakes;

namespace Ptu.Cli.Tests;

public class AvailabilityCommandTests
{
    [Fact]
    public void Availability_WithoutArguments_UsesActivePresetDefaults()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("swedencentral", result.Output);
        Assert.Contains("francecentral", result.Output);
        Assert.Contains("gpt-5.4", result.Output);
        Assert.Contains("gpt-5-mini", result.Output);
        Assert.Contains("gpt-4.1", result.Output);
        Assert.Contains("640", result.Output);
    }

    [Fact]
    public void Availability_WithExplicitRegionsAndModels_OverridesPreset()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability", "-r", "swedencentral", "-m", "gpt-4.1");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("640", result.Output);
        Assert.DoesNotContain("francecentral", result.Output);
        Assert.DoesNotContain("gpt-5.4", result.Output);
    }

    [Fact]
    public void Availability_WithCommaSeparatedValues_SplitsLists()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability", "-r", "swedencentral,francecentral", "-m", "gpt-4.1,gpt-5-mini");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("swedencentral", result.Output);
        Assert.Contains("francecentral", result.Output);
        Assert.Contains("640", result.Output);
        Assert.Contains("90", result.Output);
    }

    [Fact]
    public void Availability_MatchesRegionsAndModelsCaseInsensitively()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability", "-r", "SwedenCentral", "-m", "GPT-4.1");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("640", result.Output);
        Assert.DoesNotContain("not tracked", result.Output);
    }

    [Fact]
    public void Availability_WithNamedPreset_UsesItsRegionsAndModels()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.Presets["eu"] = new Preset { Regions = ["francecentral"], Models = ["gpt-5-mini"] };

        var result = app.Run("availability", "--preset", "eu");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("francecentral", result.Output);
        Assert.Contains("90", result.Output);
        Assert.DoesNotContain("swedencentral", result.Output);
    }

    [Fact]
    public void Availability_WithUnknownPreset_FailsWithExitCode1()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability", "--preset", "nope");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("nope", result.Output);
    }

    [Fact]
    public void Availability_DefaultsToDataZoneColumns()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability", "-r", "swedencentral", "-m", "gpt-4.1");

        Assert.Contains("Data Zone", result.Output);
        Assert.DoesNotContain("Regional", result.Output);
        Assert.DoesNotContain("Global", result.Output);
    }

    [Fact]
    public void Availability_WithRegionalType_ShowsRegionalData()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability", "-r", "francecentral", "-m", "gpt-4.1", "-t", "regional");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Regional", result.Output);
        Assert.Contains("220", result.Output);
        Assert.DoesNotContain("Data Zone", result.Output);
    }

    [Fact]
    public void Availability_WithMultipleTypes_ShowsAllRequestedColumns()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability", "-r", "swedencentral", "-m", "gpt-4.1", "-t", "datazone,global");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Data Zone", result.Output);
        Assert.Contains("Global", result.Output);
        Assert.Contains("640", result.Output);
        Assert.Contains("870", result.Output);
    }

    [Fact]
    public void Availability_WithUnknownType_FailsWithExitCode1()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability", "-t", "warp");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("warp", result.Output);
    }

    [Fact]
    public void Availability_WhenApiCallFails_ReturnsExitCode2()
    {
        var (app, _, client) = TestHost.Create();
        client.ThrowOnGet = new HttpRequestException("boom");

        var result = app.Run("availability");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("boom", result.Output);
    }

    [Fact]
    public void Availability_WhenApiStatusIsNotSucceeded_ReturnsExitCode2()
    {
        var (app, _, client) = TestHost.Create();
        client.Snapshot = FakeAvailabilityClient.CreateSnapshot(status: "failed");

        var result = app.Run("availability");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("failed", result.Output);
    }

    [Fact]
    public void Availability_WithUntrackedRegionOrModel_MarksRowsAsNotTracked()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability", "-r", "swedencentral", "-m", "o9-preview");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("not tracked", result.Output);
    }

    [Fact]
    public void Availability_GroupsRowsByModel()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability", "-r", "swedencentral,francecentral", "-m", "gpt-4.1,gpt-5-mini");

        Assert.Equal(0, result.ExitCode);
        // Model-major grouping: each model appears once (blank on repeated rows),
        // while each region appears once per model group.
        Assert.Equal(1, CountOccurrences(result.Output, "gpt-4.1"));
        Assert.Equal(1, CountOccurrences(result.Output, "gpt-5-mini"));
        Assert.Equal(2, CountOccurrences(result.Output, "swedencentral"));
        Assert.Equal(2, CountOccurrences(result.Output, "francecentral"));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [Fact]
    public void Availability_WhenPresetHasNoModels_FailsWithExitCode1()    {
        var (app, store, _) = TestHost.Create();
        store.Config.Presets["empty"] = new Preset { Regions = ["swedencentral"], Models = [] };

        var result = app.Run("availability", "--preset", "empty");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No models", result.Output);
    }
}
