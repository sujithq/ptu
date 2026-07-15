using Microsoft.Extensions.DependencyInjection;
using Ptu.Cli.Availability;
using Ptu.Cli.Configuration;
using Ptu.Cli.Infrastructure;
using Ptu.Cli.Tests.Fakes;
using Spectre.Console.Cli.Testing;

namespace Ptu.Cli.Tests;

/// <summary>Builds a <see cref="CommandAppTester"/> wired to the production configuration with fake services.</summary>
internal static class TestHost
{
    public const string TestEndpoint = "https://unit.test/api/availability/azure-ptu";

    public static (CommandAppTester App, InMemoryPresetStore Store, FakeAvailabilityClient Client) Create(
        FakePaygDataZoneClient? paygClient = null)
    {
        var store = new InMemoryPresetStore();
        store.Config.ApiEndpoint = TestEndpoint;
        var client = new FakeAvailabilityClient();
        paygClient ??= new FakePaygDataZoneClient();

        var services = new ServiceCollection();
        services.AddSingleton<IPresetStore>(store);
        services.AddSingleton<IAvailabilityClient>(client);
        services.AddSingleton<IPaygDataZoneClient>(paygClient);

        var app = new CommandAppTester(new TypeRegistrar(services));
        app.Configure(Ptu.Cli.Program.Configure);
        return (app, store, client);
    }
}
