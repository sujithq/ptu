using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Ptu.Cli.Availability;

/// <summary>Reads PAYG Standard availability from the public Microsoft Learn tables.</summary>
public sealed class HttpPaygDataZoneClient(HttpClient http) : IPaygDataZoneClient
{
    internal const string SourceUrl =
        "https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure-region-availability?pivots=standard";

    public async Task<PaygDataZoneSnapshot> GetAsync(string tab, bool refresh, CancellationToken cancellationToken)
    {
        if (!PaygDataZoneTabs.TryNormalize(tab, out var normalizedTab))
        {
            throw new ArgumentException($"Unknown Microsoft Learn region tab '{tab}'.", nameof(tab));
        }

        using var request = CreateRequest(normalizedTab, refresh);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(html, normalizedTab);
    }

    internal static HttpRequestMessage CreateRequest(string tab, bool refresh)
    {
        if (!PaygDataZoneTabs.TryNormalize(tab, out var normalizedTab))
        {
            throw new ArgumentException($"Unknown Microsoft Learn region tab '{tab}'.", nameof(tab));
        }

        var request = new HttpRequestMessage(HttpMethod.Get, $"{SourceUrl}&tabs={normalizedTab}");
        request.Headers.TryAddWithoutValidation("Accept", "text/html");

        if (refresh)
        {
            request.Headers.CacheControl = new()
            {
                NoCache = true,
                NoStore = true,
                MaxAge = TimeSpan.Zero,
            };
            request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
        }

        return request;
    }

    internal static PaygDataZoneSnapshot Parse(string html, string tab = PaygDataZoneTabs.Default)
    {
        if (!PaygDataZoneTabs.TryNormalize(tab, out var normalizedTab))
        {
            throw new ArgumentException($"Unknown Microsoft Learn region tab '{tab}'.", nameof(tab));
        }

        var document = new HtmlParser().ParseDocument(html);
        var modelsByType = new Dictionary<PtuType, IReadOnlyList<PaygDataZoneModel>>();
        foreach (var (type, sectionId, heading) in StandardSections)
        {
            var section = FindAzureOpenAiSection(document, sectionId, heading)
                ?? throw new InvalidOperationException($"Microsoft Learn no longer exposes the PAYG {PtuTypes.DisplayName(type)} Standard availability table in the expected section.");

            var tabPanels = section.QuerySelectorAll("[role=tabpanel][data-tab]");
            var selectedPanel = tabPanels.FirstOrDefault(panel =>
                string.Equals(panel.GetAttribute("data-tab"), normalizedTab, StringComparison.OrdinalIgnoreCase));
            if (tabPanels.Length > 0 && selectedPanel is null)
            {
                throw new InvalidOperationException($"Microsoft Learn returned no PAYG {PtuTypes.DisplayName(type)} Standard table for region tab '{normalizedTab}'.");
            }

            var availabilityContent = selectedPanel ?? section;
            var models = ParseTables(availabilityContent.QuerySelectorAll("table"));
            if (models.Count == 0
                && !availabilityContent.TextContent.Contains("Not available", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Microsoft Learn returned no PAYG {PtuTypes.DisplayName(type)} Standard model availability rows.");
            }

            modelsByType[type] = models;
        }

        return new PaygDataZoneSnapshot
        {
            ModelsByType = modelsByType,
            BatchModelsByType = ParseBatchSections(document, normalizedTab),
        };
    }

    /// <summary>
    /// Parses the Batch sections of the same Microsoft Learn page. Batch data is supplementary, so a
    /// missing or restructured section leaves the deployment type out of the result instead of failing
    /// the whole snapshot. A type that is absent from the result is reported as unknown, never as
    /// unavailable.
    /// </summary>
    private static Dictionary<PtuType, IReadOnlyList<PaygDataZoneModel>> ParseBatchSections(IDocument document, string normalizedTab)
    {
        var batchModelsByType = new Dictionary<PtuType, IReadOnlyList<PaygDataZoneModel>>();
        foreach (var (type, sectionId, heading) in BatchSections)
        {
            var section = FindAzureOpenAiSection(document, sectionId, heading);
            if (section is null)
            {
                continue;
            }

            var tabPanels = section.QuerySelectorAll("[role=tabpanel][data-tab]");
            var selectedPanel = tabPanels.FirstOrDefault(panel =>
                string.Equals(panel.GetAttribute("data-tab"), normalizedTab, StringComparison.OrdinalIgnoreCase));
            if (tabPanels.Length > 0 && selectedPanel is null)
            {
                continue;
            }

            var availabilityContent = selectedPanel ?? section;
            var models = ParseTables(availabilityContent.QuerySelectorAll("table"));
            if (models.Count == 0
                && !availabilityContent.TextContent.Contains("Not available", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            batchModelsByType[type] = models;
        }

        return batchModelsByType;
    }

    private static readonly (PtuType Type, string SectionId, string Heading)[] BatchSections =
    [
        (PtuType.Global, "global-batch", "Global Batch"),
        (PtuType.DataZone, "data-zone-batch", "Data Zone Batch"),
    ];

    private static readonly (PtuType Type, string SectionId, string Heading)[] StandardSections =
    [
        (PtuType.Global, "global-standard", "Global Standard"),
        (PtuType.DataZone, "data-zone-standard", "Data Zone Standard"),
        (PtuType.Regional, "standardregional", "Standard/Regional"),
    ];

    private static IElement? FindAzureOpenAiSection(IDocument document, string sectionId, string sectionHeading)
    {
        var sectionHeadingElement = document.QuerySelector($"#{sectionId}")
            ?? document.QuerySelectorAll("h2").FirstOrDefault(element =>
                string.Equals(element.TextContent.Trim(), sectionHeading, StringComparison.OrdinalIgnoreCase));
        var sectionStart = sectionHeadingElement?.ParentElement?.Children.Length == 1
            ? sectionHeadingElement.ParentElement
            : sectionHeadingElement;

        for (var current = sectionStart?.NextElementSibling; current is not null; current = current.NextElementSibling)
        {
            var heading = current.Matches("h2,h3,h4") ? current : current.QuerySelector("h2,h3,h4");
            if (heading?.TagName.Equals("H2", StringComparison.OrdinalIgnoreCase) is true)
            {
                break;
            }

            if (string.Equals(
                heading?.TextContent.Trim(),
                "Availability for Azure OpenAI in Foundry Models",
                StringComparison.OrdinalIgnoreCase))
            {
                return current.NextElementSibling;
            }
        }

        return null;
    }

    private static List<PaygDataZoneModel> ParseTables(IHtmlCollection<IElement> tables)
    {
        var models = new List<PaygDataZoneModel>();
        foreach (var table in tables)
        {
            var headers = table.QuerySelectorAll("thead th")
                .Select(cell => cell.TextContent.Trim())
                .ToArray();
            if (headers.Length < 3
                || !string.Equals(headers[0], "Model", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(headers[1], "Version", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var row in table.QuerySelectorAll("tbody tr"))
            {
                var cells = row.QuerySelectorAll("th,td");
                if (cells.Length < 2)
                {
                    continue;
                }

                var availableRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var index = 2; index < Math.Min(headers.Length, cells.Length); index++)
                {
                    if (cells[index].TextContent.Contains('✅', StringComparison.Ordinal))
                    {
                        availableRegions.Add(headers[index]);
                    }
                }

                models.Add(new PaygDataZoneModel
                {
                    Name = cells[0].TextContent.Trim(),
                    Version = cells[1].TextContent.Trim(),
                    AvailableRegions = availableRegions,
                });
            }
        }

        return models;
    }
}