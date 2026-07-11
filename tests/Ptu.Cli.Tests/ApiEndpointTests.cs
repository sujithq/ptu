using Ptu.Cli.Commands;
using Ptu.Cli.Configuration;
using Ptu.Cli.Tests.Fakes;
using Spectre.Console.Testing;

namespace Ptu.Cli.Tests;

public class ApiEndpointTests
{
    [Fact]
    public void Availability_PassesConfiguredEndpointToClient()
    {
        var (app, _, client) = TestHost.Create();

        var result = app.Run("availability");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(TestHost.TestEndpoint, client.LastEndpoint);
    }

    [Fact]
    public void Availability_WithoutEndpoint_NonInteractive_FailsWithExitCode1()
    {
        var (app, store, client) = TestHost.Create();
        store.Config.ApiEndpoint = null;

        var result = app.Run("availability");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("endpoint", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Null(client.LastEndpoint);
    }

    [Fact]
    public void ResolveOrPromptEndpoint_WhenConfigured_ReturnsStoredValueWithoutSaving()
    {
        var console = new TestConsole();
        var store = new InMemoryPresetStore();
        store.Config.ApiEndpoint = "https://stored.test/api";

        var endpoint = AvailabilityCommand.ResolveOrPromptEndpoint(console, store, store.Config);

        Assert.Equal("https://stored.test/api", endpoint);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void ResolveOrPromptEndpoint_FirstUseInteractive_PromptsAndStoresInput()
    {
        var console = new TestConsole().Interactive();
        console.Input.PushTextWithEnter("https://example.test/api");
        var store = new InMemoryPresetStore();

        var endpoint = AvailabilityCommand.ResolveOrPromptEndpoint(console, store, store.Config);

        Assert.Equal("https://example.test/api", endpoint);
        Assert.Equal("https://example.test/api", store.Config.ApiEndpoint);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void ResolveOrPromptEndpoint_FirstUseWithInvalidInput_ReasksUntilValid()
    {
        var console = new TestConsole().Interactive();
        console.Input.PushTextWithEnter("not-a-url");
        console.Input.PushTextWithEnter("https://example.test/api");
        var store = new InMemoryPresetStore();

        var endpoint = AvailabilityCommand.ResolveOrPromptEndpoint(console, store, store.Config);

        Assert.Equal("https://example.test/api", endpoint);
        Assert.Equal("https://example.test/api", store.Config.ApiEndpoint);
    }

    [Fact]
    public void ResolveOrPromptEndpoint_NonInteractiveWithoutEndpoint_ReturnsNull()
    {
        var console = new TestConsole();
        var store = new InMemoryPresetStore();

        var endpoint = AvailabilityCommand.ResolveOrPromptEndpoint(console, store, store.Config);

        Assert.Null(endpoint);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void PresetResetAll_PreservesApiEndpoint()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.Presets["eu"] = new Preset { Regions = ["francecentral"], Models = ["gpt-4.1"] };

        var result = app.Run("preset", "reset", "--all");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(TestHost.TestEndpoint, store.Config.ApiEndpoint);
        Assert.Single(store.Config.Presets);
    }

    [Fact]
    public void EndpointShow_WhenConfigured_PrintsEndpoint()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("endpoint", "show");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(TestHost.TestEndpoint, result.Output);
    }

    [Fact]
    public void EndpointShow_WhenNotConfigured_SaysNotConfigured()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.ApiEndpoint = null;

        var result = app.Run("endpoint", "show");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("not configured", result.Output);
        Assert.Contains("endpoint set", result.Output);
    }

    [Fact]
    public void EndpointSet_WithValidUrl_StoresEndpoint()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("endpoint", "set", "https://other.example/api");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("https://other.example/api", store.Config.ApiEndpoint);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void EndpointSet_WithInvalidUrl_FailsWithExitCode1()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("endpoint", "set", "not-a-url");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not-a-url", result.Output);
        Assert.Equal(TestHost.TestEndpoint, store.Config.ApiEndpoint);
        Assert.Equal(0, store.SaveCount);
    }
}
