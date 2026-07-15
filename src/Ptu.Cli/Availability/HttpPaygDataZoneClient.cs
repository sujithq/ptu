using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Ptu.Cli.Availability;

/// <summary>Reads PAYG Data Zone Standard availability from the public Microsoft Learn table.</summary>
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
        var section = FindAzureOpenAiDataZoneSection(document)
            ?? throw new InvalidOperationException("Microsoft Learn no longer exposes the PAYG Data Zone Standard availability table in the expected section.");

        var tabPanels = section.QuerySelectorAll("[role=tabpanel][data-tab]");
        var selectedPanel = tabPanels.FirstOrDefault(panel =>
            string.Equals(panel.GetAttribute("data-tab"), normalizedTab, StringComparison.OrdinalIgnoreCase));
        if (tabPanels.Length > 0 && selectedPanel is null)
        {
            throw new InvalidOperationException($"Microsoft Learn returned no PAYG Data Zone Standard table for region tab '{normalizedTab}'.");
        }

        var availabilityContent = selectedPanel ?? section;
        var models = ParseTables(availabilityContent.QuerySelectorAll("table"));
        if (models.Count == 0
            && !availabilityContent.TextContent.Contains("Not available", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Microsoft Learn returned no PAYG Data Zone Standard model availability rows.");
        }

        return new PaygDataZoneSnapshot { Models = models };
    }

    private static IElement? FindAzureOpenAiDataZoneSection(IDocument document)
    {
        var dataZoneHeading = document.QuerySelector("#data-zone-standard");
        var sectionStart = dataZoneHeading?.ParentElement?.Children.Length == 1
            ? dataZoneHeading.ParentElement
            : dataZoneHeading;

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