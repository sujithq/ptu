using Ptu.Cli.Configuration;
using Ptu.Cli.Tests.Fakes;

namespace Ptu.Cli.Tests;

public class AvailabilityCommandTests
{
    [Fact]
    public void Availability_WithoutArguments_UsesActivePresetDefaults()
    {
        var paygClient = new FakePaygDataZoneClient();
        var (app, _, _) = TestHost.Create(paygClient);

        var result = app.Run("availability");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("swedencentral", result.Output);
        Assert.Contains("francecentral", result.Output);
        Assert.Contains("gpt-5.4", result.Output);
        Assert.Contains("gpt-5-mini", result.Output);
        Assert.Contains("gpt-4.1", result.Output);
        Assert.Contains("640", result.Output);
        Assert.Equal("az-europe", paygClient.LastTab);
        Assert.Contains("PAYG geography tab: az-europe", result.Output);
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
    public void Availability_WithRefresh_RequestsFreshData()
    {
        var paygClient = new FakePaygDataZoneClient();
        var (app, _, client) = TestHost.Create(paygClient);

        var result = app.Run("availability", "--refresh");

        Assert.Equal(0, result.ExitCode);
        Assert.True(client.LastRefresh);
        Assert.True(paygClient.LastRefresh);
    }

    [Fact]
    public void Availability_WithoutRefresh_DoesNotRequestFreshData()
    {
        var (app, _, client) = TestHost.Create();

        var result = app.Run("availability");

        Assert.Equal(0, result.ExitCode);
        Assert.False(client.LastRefresh);
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
        var paygClient = new FakePaygDataZoneClient();
        var (app, store, _) = TestHost.Create(paygClient);
        store.Config.Presets["us"] = new Preset { Regions = ["eastus"], Models = ["gpt-5-mini"], Tab = "az-americas" };

        var result = app.Run("availability", "--preset", "us");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("eastus", result.Output);
        Assert.DoesNotContain("swedencentral", result.Output);
        Assert.Equal("az-americas", paygClient.LastTab);
    }

    [Fact]
    public void Availability_WithExplicitTab_OverridesPresetTab()
    {
        var paygClient = new FakePaygDataZoneClient();
        var (app, store, _) = TestHost.Create(paygClient);
        store.Config.Presets["apac"] = new Preset { Regions = ["japaneast"], Models = ["gpt-4.1"], Tab = "az-apac" };

        var result = app.Run("availability", "--preset", "apac", "--tab", "AZ-MEA");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("az-mea", paygClient.LastTab);
        Assert.Contains("PAYG geography tab: az-mea", result.Output);
    }

    [Fact]
    public void Availability_WithUnknownTab_FailsBeforeCallingApis()
    {
        var paygClient = new FakePaygDataZoneClient();
        var (app, _, client) = TestHost.Create(paygClient);

        var result = app.Run("availability", "--tab", "az-atlantis");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("az-atlantis", result.Output);
        Assert.Contains("az-americas, az-europe, az-apac, az-mea", result.Output);
        Assert.Null(client.LastEndpoint);
        Assert.Equal(0, paygClient.CallCount);
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
        Assert.Contains("PAYG Data Zone", result.Output);
        Assert.DoesNotContain("Regional", result.Output);
        Assert.DoesNotContain("Global", result.Output);
    }

    [Fact]
    public void Availability_WhenPaygDataZoneIsDocumented_ShowsYes()
    {
        var paygClient = new FakePaygDataZoneClient();
        var (app, _, _) = TestHost.Create(paygClient);

        var result = app.Run("availability", "-r", "swedencentral", "-m", "gpt-4.1");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, CountOccurrences(result.Output, "yes"));
        Assert.Equal(1, paygClient.CallCount);
    }

    [Fact]
    public void Availability_WhenPaygDataZoneIsNotDocumented_ShowsNo()
    {
        var paygClient = new FakePaygDataZoneClient
        {
            Snapshot = new()
            {
                Models = [FakePaygDataZoneClient.Model("gpt-4.1", "2025-04-14", "francecentral")],
            },
        };
        var (app, _, _) = TestHost.Create(paygClient);

        var result = app.Run("availability", "-r", "swedencentral", "-m", "gpt-4.1");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, CountOccurrences(result.Output, "yes"));
        Assert.Equal(1, CountOccurrences(result.Output, "no"));
    }

    [Fact]
    public void Availability_WhenPaygSourceFails_ShowsUnknownAndKeepsPtuResult()
    {
        var paygClient = new FakePaygDataZoneClient { ThrowOnGet = new HttpRequestException("docs unavailable") };
        var (app, _, _) = TestHost.Create(paygClient);

        var result = app.Run("availability", "-r", "swedencentral", "-m", "gpt-4.1");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Warning", result.Output);
        Assert.Contains("docs unavailable", result.Output);
        Assert.Contains("unknown", result.Output);
        Assert.Contains("640", result.Output);
    }

    [Fact]
    public void Availability_WithRegionalType_ShowsRegionalData()
    {
        var paygClient = new FakePaygDataZoneClient();
        var (app, _, _) = TestHost.Create(paygClient);

        var result = app.Run("availability", "-r", "francecentral", "-m", "gpt-4.1", "-t", "regional");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Regional", result.Output);
        Assert.Contains("220", result.Output);
        Assert.DoesNotContain("Data Zone", result.Output);
        Assert.Equal(0, paygClient.CallCount);
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
    public void Availability_WhenPresetHasNoModels_FailsWithExitCode1()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.Presets["empty"] = new Preset { Regions = ["swedencentral"], Models = [] };

        var result = app.Run("availability", "--preset", "empty");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No models", result.Output);
    }
}
