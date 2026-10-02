using System.ComponentModel;
using System.Globalization;
using System.Net;
using Ptu.Cli.Availability;
using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace Ptu.Cli.Commands;

public sealed class AvailabilityCommand(
    IAnsiConsole console,
    IPresetStore store,
    IAvailabilityClient client,
    IPaygDataZoneClient paygClient,
    IPaygQuotaClient quotaClient)
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

        [CommandOption("--show-quota")]
        [Description("Show documented PAYG Standard RPM and TPM limits for each quota tier.")]
        public bool ShowQuota { get; init; }

        [CommandOption("--quota-layout <LAYOUT>")]
        [Description("Quota table layout: tier (default) or single.")]
        public string QuotaLayout { get; init; } = "tier";
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

        if (!TryParseQuotaLayout(settings.QuotaLayout, out var quotaLayout))
        {
            console.MarkupLineInterpolated($"[red]Error:[/] Unknown quota layout '{settings.QuotaLayout}'. Valid values: tier, single.");
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

        PaygQuotaSnapshot? quotaSnapshot = null;
        if (settings.ShowQuota)
        {
            try
            {
                quotaSnapshot = await quotaClient.GetAsync(settings.Refresh, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                console.MarkupLineInterpolated($"[yellow]Warning:[/] PAYG quota limits could not be retrieved from Microsoft Learn: {ex.Message}");
            }
        }

        var availabilityTable = BuildTable(snapshot, paygSnapshot, regions, models, types);
        var availabilityOutput = new Rows(
            new Markup("[bold]Model availability[/]"),
            availabilityTable);
        if (quotaSnapshot is null)
        {
            console.Write(availabilityOutput);
        }
        else
        {
            console.Write(new Columns(
            [
                availabilityOutput,
                BuildQuotaTables(quotaSnapshot, models, types, quotaLayout),
            ])
            {
                Expand = false,
                Padding = new Padding(0, 0, 2, 0),
            });
        }

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
        console.MarkupLine("[grey]PTU: OK = supported with positive capacity; NC = supported, capacity is 0; NS = API explicitly says unsupported; ? = missing/unusable data; NT = model/region absent from API. '-' capacity = not reported.[/]");
        console.MarkupLine("[grey]PAYG Standard: yes = at least one documented model version is available in the region; no = no documented version is listed as available; unknown = Microsoft Learn data could not be retrieved or parsed.[/]");
        console.MarkupLine("[grey]PAYG Batch: same meaning as PAYG Standard, for Batch deployments; unknown also covers a missing or restructured Batch section. Microsoft Learn documents Batch for Global and Data zone only, so no Batch column is shown for Regional.[/]");
    }

    private static bool TryParseQuotaLayout(string value, out QuotaLayout layout)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "tier":
                layout = QuotaLayout.Tier;
                return true;
            case "single":
                layout = QuotaLayout.Single;
                return true;
            default:
                layout = default;
                return false;
        }
    }

    private static IRenderable BuildQuotaTables(
        PaygQuotaSnapshot snapshot,
        List<string> models,
        List<PtuType> types,
        QuotaLayout layout)
    {
        var renderables = new List<IRenderable>
        {
            new Markup("[bold]PAYG Standard quota limits by tier[/]"),
        };

        var matchingLimits = snapshot.Tiers
            .SelectMany(tier => tier.Limits.Select(limit => (Tier: tier.Name, Limit: limit)))
            .Where(item =>
                models.Contains(item.Limit.Model, StringComparer.OrdinalIgnoreCase)
                && types.Contains(item.Limit.Type))
            .ToList();

        if (layout is QuotaLayout.Single)
        {
            if (matchingLimits.Count == 0)
            {
                renderables.Add(new Markup("[yellow]No documented quota limits matched the selected models and deployment types.[/]"));
            }
            else
            {
                renderables.Add(BuildSingleQuotaTable(matchingLimits));
            }
        }
        else
        {
            var wroteTier = false;
            foreach (var tier in snapshot.Tiers)
            {
                var limits = matchingLimits
                    .Where(item => string.Equals(item.Tier, tier.Name, StringComparison.Ordinal))
                    .Select(item => item.Limit)
                    .ToList();
                if (limits.Count == 0)
                {
                    continue;
                }

                wroteTier = true;
                renderables.Add(new Markup($"[bold]{Markup.Escape(tier.Name)}[/]"));

                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn("Model");
                table.AddColumn("Deployment type");
                table.AddColumn(new TableColumn("RPM").RightAligned());
                table.AddColumn(new TableColumn("TPM").RightAligned());

                foreach (var limit in limits)
                {
                    table.AddRow(
                        Markup.Escape(limit.Model),
                        Markup.Escape($"{PtuTypes.DisplayName(limit.Type)} Standard"),
                        Markup.Escape(limit.RequestsPerMinute),
                        Markup.Escape(limit.TokensPerMinute));
                }

                renderables.Add(table);
            }

            if (!wroteTier)
            {
                renderables.Add(new Markup("[yellow]No documented quota limits matched the selected models and deployment types.[/]"));
            }
        }

        renderables.Add(new Markup("[grey]Quota limits are scoped by subscription and deployment type; they are not regional capacity values.[/]"));
        return new Rows(renderables);
    }

    private static Table BuildSingleQuotaTable(
        List<(string Tier, PaygQuotaLimit Limit)> limits)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Tier");
        table.AddColumn("Model");
        table.AddColumn("Deployment type");
        table.AddColumn(new TableColumn("RPM").RightAligned());
        table.AddColumn(new TableColumn("TPM").RightAligned());

        foreach (var (tier, limit) in limits)
        {
            table.AddRow(
                Markup.Escape(tier),
                Markup.Escape(limit.Model),
                Markup.Escape($"{PtuTypes.DisplayName(limit.Type)} Standard"),
                Markup.Escape(limit.RequestsPerMinute),
                Markup.Escape(limit.TokensPerMinute));
        }

        return table;
    }

    private enum QuotaLayout
    {
        Tier,
        Single,
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
        var compactGroups = types.Count > 1;
        table.AddColumn(new TableColumn("Model") { Padding = new Padding(0) });
        table.AddColumn(new TableColumn("Region") { Padding = new Padding(0) });
        foreach (var type in types)
        {
            table.AddColumn(new TableColumn(PtuTypes.DisplayName(type))
            {
                Alignment = Justify.Center,
                Padding = new Padding(0),
                Width = GetDeploymentWidth(type, compactGroups),
            });
        }

        var subheaderCells = new List<IRenderable>
        {
            new Text(string.Empty),
            new Text(string.Empty),
        };
        subheaderCells.AddRange(types.Select(type => BuildDeploymentSubheader(type, compactGroups)));
        table.AddRow(subheaderCells);

        foreach (var model in models)
        {
            var firstRowOfGroup = true;
            foreach (var region in regions)
            {
                var modelData = snapshot.FindRegion(region)?.FindModel(model);
                var cells = new List<IRenderable>
                {
                    new Text(firstRowOfGroup ? model : string.Empty),
                    new Text(region),
                };

                foreach (var type in types)
                {
                    string ptuStatus;
                    string capacity;
                    if (modelData is null)
                    {
                        ptuStatus = "[grey]NT[/]";
                        capacity = "[grey]-[/]";
                    }
                    else
                    {
                        var offer = modelData.Offers[type];
                        ptuStatus = (offer.Available, offer.Capacity) switch
                        {
                            (false, _) => "[grey]NS[/]",
                            (true, 0) => "[yellow]NC[/]",
                            (true, > 0) => "[green]OK[/]",
                            _ => "[yellow]?[/]",
                        };
                        capacity = offer.Capacity?.ToString(CultureInfo.InvariantCulture) ?? "-";
                    }

                    var standard = paygSnapshot is null
                        ? "[yellow]unknown[/]"
                        : paygSnapshot.IsAvailable(type, model, region) ? "[green]yes[/]" : "[red]no[/]";

                    string? batch = null;
                    if (PaygDataZoneSnapshot.SupportsBatch(type))
                    {
                        batch = paygSnapshot?.GetBatchAvailability(type, model, region) switch
                        {
                            true => "[green]yes[/]",
                            false => "[red]no[/]",
                            null => "[yellow]unknown[/]",
                        };
                    }

                    cells.Add(BuildDeploymentGrid(type, ptuStatus, capacity, standard, batch, compactGroups));
                }

                table.AddRow(cells.ToArray());
                firstRowOfGroup = false;
            }
        }

        return table;
    }

    private static Grid BuildDeploymentSubheader(PtuType type, bool compact) =>
        BuildDeploymentGrid(
            type,
            "[bold]PTU[/]",
            compact ? "[bold]Cap[/]" : "[bold]Capacity[/]",
            "[bold]PAYG[/]",
            PaygDataZoneSnapshot.SupportsBatch(type)
                ? compact ? "[bold]Bat[/]" : "[bold]Batch[/]"
                : null,
            compact);

    private static Grid BuildDeploymentGrid(
        PtuType type,
        string ptu,
        string capacity,
        string standard,
        string? batch,
        bool compact)
    {
        var supportsBatch = PaygDataZoneSnapshot.SupportsBatch(type);
        var grid = new Grid();
        grid.AddColumn(new GridColumn
        {
            Width = 3,
            NoWrap = true,
            Padding = new Padding(0),
            Alignment = Justify.Center,
        });
        grid.AddColumn(SeparatorColumn());
        grid.AddColumn(new GridColumn
        {
            Width = compact ? 5 : 8,
            NoWrap = true,
            Padding = new Padding(0),
            Alignment = Justify.Right,
        });
        grid.AddColumn(SeparatorColumn());
        grid.AddColumn(new GridColumn
        {
            Width = 4,
            NoWrap = true,
            Padding = new Padding(0),
            Alignment = Justify.Center,
        });

        if (supportsBatch)
        {
            grid.AddColumn(SeparatorColumn());
            grid.AddColumn(new GridColumn
            {
                Width = compact ? 3 : 7,
                NoWrap = true,
                Padding = new Padding(0),
                Alignment = Justify.Center,
            });
        }

        const string separator = "[grey]│[/]";
        grid.AddRow(batch is null
            ? [ptu, separator, capacity, separator, standard]
            : [ptu, separator, capacity, separator, standard, separator, batch]);
        return grid;
    }

    private static GridColumn SeparatorColumn() => new()
    {
        Width = 1,
        NoWrap = true,
        Padding = new Padding(0),
        Alignment = Justify.Center,
    };

    private static int GetDeploymentWidth(PtuType type, bool compact) =>
        (compact, PaygDataZoneSnapshot.SupportsBatch(type)) switch
        {
            (true, true) => 18,
            (true, false) => 14,
            (false, true) => 25,
            (false, false) => 17,
        };
}
