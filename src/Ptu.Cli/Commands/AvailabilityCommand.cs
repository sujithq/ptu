using System.ComponentModel;
using System.Globalization;
using System.Net;
using Ptu.Cli.Availability;
using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands;

public sealed class AvailabilityCommand(
    IAnsiConsole console,
    IPresetStore store,
    IAvailabilityClient client,
    IPaygDataZoneClient paygClient)
    : AsyncCommand<AvailabilityCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-r|--region <REGION>")]
        [Description("Region(s) to check. Repeatable or comma-separated. Overrides the preset.")]
        public string[] Regions { get; init; } = [];

        [CommandOption("-m|--model <MODEL>")]
        [Description("Model(s) to check. Repeatable or comma-separated. Overrides the preset.")]
        public string[] Models { get; init; } = [];

        [CommandOption("-p|--preset <NAME>")]
        [Description("Preset supplying default regions, models, and Learn tab. Defaults to the active preset.")]
        public string? Preset { get; init; }

        [CommandOption("--tab <TAB>")]
        [Description("Microsoft Learn PAYG geography: az-americas, az-europe, az-apac, or az-mea. Overrides the preset.")]
        public string? Tab { get; init; }

        [CommandOption("-t|--type <TYPE>")]
        [Description("PTU type(s) to show: datazone, regional, or global. Defaults to datazone.")]
        public string[] Types { get; init; } = [];

        [CommandOption("--refresh")]
        [Description("Bypass caches and retrieve fresh PTU and PAYG data.")]
        public bool Refresh { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        PresetConfig config;
        try
        {
            config = store.Load();
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
            return 1;
        }

        var presetName = settings.Preset ?? config.DefaultPreset;
        if (!config.Presets.TryGetValue(presetName, out var preset))
        {
            console.MarkupLineInterpolated($"[red]Error:[/] Unknown preset '{presetName}'. Run 'ptu preset list' to see available presets.");
            return 1;
        }

        var regions = CommandInput.Normalize(settings.Regions);
        if (regions.Count == 0)
        {
            regions = CommandInput.Normalize(preset.Regions);
        }

        var models = CommandInput.Normalize(settings.Models);
        if (models.Count == 0)
        {
            models = CommandInput.Normalize(preset.Models);
        }

        if (regions.Count == 0)
        {
            console.MarkupLineInterpolated($"[red]Error:[/] No regions specified. Pass --region or add regions to the '{presetName}' preset.");
            return 1;
        }

        if (models.Count == 0)
        {
            console.MarkupLineInterpolated($"[red]Error:[/] No models specified. Pass --model or add models to the '{presetName}' preset.");
            return 1;
        }

        if (!PaygDataZoneTabs.TryNormalize(settings.Tab ?? preset.Tab, out var tab))
        {
            console.MarkupLineInterpolated($"[red]Error:[/] Unknown Microsoft Learn region tab '{settings.Tab ?? preset.Tab}'. Valid values: {string.Join(", ", PaygDataZoneTabs.All)}.");
            return 1;
        }

        var types = new List<PtuType>();
        foreach (var raw in settings.Types.SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            if (!PtuTypes.TryParse(raw, out var type))
            {
                console.MarkupLineInterpolated($"[red]Error:[/] Unknown PTU type '{raw}'. Valid values: datazone, regional, global.");
                return 1;
            }

            if (!types.Contains(type))
            {
                types.Add(type);
            }
        }

        if (types.Count == 0)
        {
            types.Add(PtuType.DataZone);
        }

        var endpoint = ResolveOrPromptEndpoint(console, store, config);
        if (endpoint is null)
        {
            return 1;
        }

        AvailabilitySnapshot snapshot;
        try
        {
            snapshot = await client.GetAsync(endpoint, config.AuthCookie, settings.Refresh, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            console.MarkupLineInterpolated($"[red]Error:[/] The availability API rejected the request ({(int)ex.StatusCode} {ex.StatusCode}).");
            console.MarkupLine(config.AuthCookie is null
                ? "The API requires authentication. Copy the session cookie from your browser's DevTools and run [blue]ptu auth set \"<name>=<value>\"[/]."
                : "The stored session cookie was not accepted (it may have expired). Refresh it with [blue]ptu auth set \"<name>=<value>\"[/].");
            return 2;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            console.MarkupLineInterpolated($"[red]Error:[/] Failed to query the availability API: {ex.Message}");
            return 2;
        }

        if (!string.Equals(snapshot.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            console.MarkupLineInterpolated($"[red]Error:[/] The availability API reported status '{snapshot.Status}'.");
            return 2;
        }

        PaygDataZoneSnapshot? paygSnapshot = null;
        if (types.Contains(PtuType.DataZone))
        {
            try
            {
                paygSnapshot = await paygClient.GetAsync(tab, settings.Refresh, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                console.MarkupLineInterpolated($"[yellow]Warning:[/] PAYG Data Zone availability could not be retrieved from Microsoft Learn: {ex.Message}");
            }
        }

        console.Write(BuildTable(snapshot, paygSnapshot, regions, models, types));
        if (types.Contains(PtuType.DataZone))
        {
            console.MarkupLineInterpolated($"[grey]PAYG geography tab: {tab}[/]");
        }

        if (snapshot.GeneratedAt is { } generatedAt)
        {
            console.MarkupLineInterpolated($"[grey]Data generated at {generatedAt.ToString("u", CultureInfo.InvariantCulture)}[/]");
        }

        return 0;
    }

    /// <summary>
    /// Returns the configured API endpoint. On first use it prompts for the endpoint and stores it;
    /// in non-interactive sessions it reports an error and returns null.
    /// </summary>
    internal static string? ResolveOrPromptEndpoint(IAnsiConsole console, IPresetStore store, PresetConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiEndpoint))
        {
            return config.ApiEndpoint;
        }

        if (!console.Profile.Capabilities.Interactive)
        {
            console.MarkupLine("[red]Error:[/] No availability API endpoint configured. Run 'ptu availability' once in an interactive terminal to set it.");
            return null;
        }

        var endpoint = console.Prompt(
            new TextPrompt<string>("Availability API endpoint:")
                .Validate(value =>
                    CommandInput.IsValidHttpUrl(value)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Enter an absolute http(s) URL.")));

        config.ApiEndpoint = endpoint;
        store.Save(config);
        console.MarkupLine("[grey]Endpoint saved to configuration.[/]");
        return endpoint;
    }

    private static Table BuildTable(
        AvailabilitySnapshot snapshot,
        PaygDataZoneSnapshot? paygSnapshot,
        List<string> regions,
        List<string> models,
        List<PtuType> types)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Model");
        table.AddColumn("Region");
        foreach (var type in types)
        {
            table.AddColumn(new TableColumn($"{PtuTypes.DisplayName(type)} PTU").Centered());
            table.AddColumn(new TableColumn($"{PtuTypes.DisplayName(type)} capacity").RightAligned());
            if (type == PtuType.DataZone)
            {
                table.AddColumn(new TableColumn("PAYG Data Zone").Centered());
            }
        }

        foreach (var model in models)
        {
            var firstRowOfGroup = true;
            foreach (var region in regions)
            {
                var modelData = snapshot.FindRegion(region)?.FindModel(model);
                var cells = new List<string>
                {
                    firstRowOfGroup ? Markup.Escape(model) : string.Empty,
                    Markup.Escape(region),
                };

                foreach (var type in types)
                {
                    if (modelData is null)
                    {
                        cells.Add("[grey]not tracked[/]");
                        cells.Add("[grey]-[/]");
                    }
                    else
                    {
                        var offer = modelData.Offers[type];
                        cells.Add(offer.Available ? "[green]yes[/]" : "[red]no[/]");
                        cells.Add(offer.Capacity?.ToString(CultureInfo.InvariantCulture) ?? "-");
                    }

                    if (type == PtuType.DataZone)
                    {
                        cells.Add(paygSnapshot is null
                            ? "[yellow]unknown[/]"
                            : paygSnapshot.IsAvailable(model, region) ? "[green]yes[/]" : "[red]no[/]");
                    }
                }

                table.AddRow(cells.ToArray());
                firstRowOfGroup = false;
            }
        }

        return table;
    }
}
