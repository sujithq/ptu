using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Ptu.Cli.Availability;
using Ptu.Cli.Commands;
using Ptu.Cli.Commands.Endpoint;
using Ptu.Cli.Commands.Presets;
using Ptu.Cli.Configuration;
using Ptu.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Banner once per terminal session, never into pipes, and not when help
        // is about to render it anyway (see BannerHelpProvider).
        var firstRunInSession = !Console.IsOutputRedirected && new FileSessionMarker().TryMarkFirstRun();
        if (firstRunInSession && !IsHelpInvocation(args))
        {
            WriteBanner(AnsiConsole.Console);
        }

        var services = new ServiceCollection();
        ConfigureServices(services);

        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(Configure);
        return await app.RunAsync(args);
    }

    /// <summary>Writes the FIGlet startup banner.</summary>
    public static void WriteBanner(IAnsiConsole console) => console.Write(CreateBanner());

    /// <summary>The ptu FIGlet banner, shared by the startup path and help output.</summary>
    internal static FigletText CreateBanner() => new FigletText("ptu").LeftJustified().Color(Color.DodgerBlue1);

    /// <summary>True when the invocation renders help (no command, -h, or --help).</summary>
    internal static bool IsHelpInvocation(string[] args) =>
        args.Length == 0 || args.Contains("-h") || args.Contains("--help");

    /// <summary>Production service registrations. Tests register fakes instead.</summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPresetStore>(_ => new FilePresetStore());
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        services.AddSingleton<IAvailabilityClient, HttpAvailabilityClient>();
    }

    /// <summary>
    /// Shared app configuration, reused by tests via <c>CommandAppTester</c>.
    /// </summary>
    public static void Configure(IConfigurator config)
    {
        config.SetApplicationName("ptu");
        config.SetApplicationVersion(GetVersion());
        config.SetHelpProvider(new BannerHelpProvider(config.Settings));

        config.AddCommand<AvailabilityCommand>("availability")
            .WithDescription("Show PTU availability for regions and models (data zone by default).")
            .WithExample("availability")
            .WithExample("availability", "-r", "swedencentral,francecentral", "-m", "gpt-4.1")
            .WithExample("availability", "--preset", "eu", "--type", "datazone,global");

        config.AddBranch("endpoint", endpoint =>
        {
            endpoint.SetDescription("Show or update the availability API endpoint.");

            endpoint.AddCommand<EndpointShowCommand>("show")
                .WithDescription("Show the configured availability API endpoint.");

            endpoint.AddCommand<EndpointSetCommand>("set")
                .WithDescription("Set the availability API endpoint.")
                .WithExample("endpoint", "set", "https://your-availability-api.example.com/api/availability/azure-ptu");
        });

        config.AddBranch("preset", preset =>
        {
            preset.SetDescription("Manage region/model presets and the active default.");

            preset.AddCommand<PresetListCommand>("list")
                .WithDescription("List all presets.");

            preset.AddCommand<PresetShowCommand>("show")
                .WithDescription("Show a preset's regions and models.")
                .WithExample("preset", "show", "eu");

            preset.AddCommand<PresetSetCommand>("set")
                .WithDescription("Create a preset or update its regions/models.")
                .WithExample("preset", "set", "eu", "--regions", "swedencentral,francecentral", "--models", "gpt-4.1");

            preset.AddCommand<PresetUseCommand>("use")
                .WithDescription("Make a preset the active default.");

            preset.AddCommand<PresetRemoveCommand>("remove")
                .WithDescription("Remove a preset (the 'default' preset is protected).");

            preset.AddCommand<PresetResetCommand>("reset")
                .WithDescription("Restore the 'default' preset to factory values and make it active.");
        });
    }

    private static string GetVersion()
    {
        var version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

        // Strip build metadata (e.g. "+<commit>") added by source link.
        var plus = version.IndexOf('+');
        return plus < 0 ? version : version[..plus];
    }
}
