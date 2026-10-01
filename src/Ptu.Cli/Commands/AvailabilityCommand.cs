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
        [Description("PTU and PAYG Standard type(s) to show: datazone, regional, or global. Defaults to datazone.")]
        public string[] Types { get; init; } = [];

        [CommandOption("--refresh")]
        [Description("Bypass caches and retrieve fresh PTU and PAYG data.")]
        public bool Refresh { get; init; }

        [CommandOption("--available-only")]
        [Description("Show only model and region rows with PTU or PAYG availability.")]
        public bool AvailableOnly { get; init; }
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

        IEnumerable<string> typesToParse = settings.Types.Length > 0 ? settings.Types : preset.Types;
        if (!PtuTypes.TryParseMany(typesToParse, out var types, out var invalidType))
        {
            console.MarkupLineInterpolated($"[red]Error:[/] Unknown PTU/PAYG type '{invalidType}'. Valid values: datazone, regional, global.");
            return 1;
        }

        if (types.Count == 0)
        {
            console.MarkupLine("[red]Error:[/] Select at least one PTU/PAYG type: datazone, regional, or global.");
            return 1;
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

        var apiSucceeded = string.Equals(snapshot.Status, "succeeded", StringComparison.OrdinalIgnoreCase);
        var canUseCachedData = string.Equals(snapshot.Status, "failed", StringComparison.OrdinalIgnoreCase)
            && snapshot.Regions.Count > 0
            && snapshot.GeneratedAt.HasValue;

        if (!apiSucceeded && !canUseCachedData)
        {
            console.MarkupLineInterpolated($"[red]Error:[/] The availability API reported status '{snapshot.Status}'.");
            return 2;
        }

        if (canUseCachedData)
        {
            var cachedAt = snapshot.GeneratedAt.GetValueOrDefault().ToString("u", CultureInfo.InvariantCulture);
            console.MarkupLineInterpolated($"[yellow]Warning:[/] The availability API reported status '{snapshot.Status}'. Showing cached data generated at {cachedAt}.");
        }

        PaygDataZoneSnapshot? paygSnapshot = null;
        try
        {
            paygSnapshot = await paygClient.GetAsync(tab, settings.Refresh, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            console.MarkupLineInterpolated($"[yellow]Warning:[/] PAYG Standard availability could not be retrieved from Microsoft Learn: {ex.Message}");
        }

        console.Write(BuildTable(snapshot, paygSnapshot, regions, models, types, settings.AvailableOnly));
        WriteStatusLegend(console);
        console.MarkupLineInterpolated($"[grey]PAYG geography tab: {tab}[/]");

        if (snapshot.GeneratedAt is { } generatedAt)
        {
            console.MarkupLineInterpolated($"[grey]Data generated at {generatedAt.ToString("u", CultureInfo.InvariantCulture)}[/]");
        }

        return 0;
    }

    private static void WriteStatusLegend(IAnsiConsole console)
    {
        console.MarkupLine("[grey]Status legend:[/]");
        console.MarkupLine("[grey]PTU: yes = supported with positive capacity; no capacity = supported, capacity is 0; not supported = API explicitly says unsupported; unknown = missing/unusable data; not tracked = model/region absent from API. '-' capacity = not reported.[/]");
        console.MarkupLine("[grey]PAYG Standard: yes = at least one documented model version is available in the region; no = no documented version is listed as available; unknown = Microsoft Learn data could not be retrieved or parsed.[/]");
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
        List<PtuType> types,
        bool availableOnly)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Model");
        table.AddColumn("Region");
        foreach (var type in types)
        {
            table.AddColumn(new TableColumn($"{PtuTypes.DisplayName(type)} PTU").Centered());
            table.AddColumn(new TableColumn($"{PtuTypes.DisplayName(type)} capacity").RightAligned());
            table.AddColumn(new TableColumn($"PAYG {PtuTypes.DisplayName(type)} Standard").Centered());
        }

        foreach (var model in models)
        {
            var firstRowOfGroup = true;
            foreach (var region in regions)
            {
                var modelData = snapshot.FindRegion(region)?.FindModel(model);
                var rowIsAvailable = types.Any(type =>
                    modelData?.Offers[type].Available == true
                    || paygSnapshot?.IsAvailable(type, model, region) == true);
                if (availableOnly && !rowIsAvailable)
                {
                    continue;
                }

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
                        cells.Add((offer.Available, offer.Capacity) switch
                        {
                            (false, _) => "[grey]not supported[/]",
                            (true, 0) => "[yellow]no capacity[/]",
                            (true, > 0) => "[green]yes[/]",
                            _ => "[yellow]unknown[/]",
                        });
                        cells.Add(offer.Capacity?.ToString(CultureInfo.InvariantCulture) ?? "-");
                    }

                    cells.Add(paygSnapshot is null
                        ? "[yellow]unknown[/]"
                        : paygSnapshot.IsAvailable(type, model, region) ? "[green]yes[/]" : "[red]no[/]");
                }

                table.AddRow(cells.ToArray());
                firstRowOfGroup = false;
            }
        }

        return table;
    }
}
