using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Ptu.Cli.Availability;

/// <summary>Reads PAYG Standard quota-tier limits from the public Microsoft Learn tables.</summary>
public sealed class HttpPaygQuotaClient(HttpClient http) : IPaygQuotaClient
{
    internal const string SourceUrl =
        "https://learn.microsoft.com/en-us/azure/foundry/openai/quotas-limits#quota-tier-reference";

    public async Task<PaygQuotaSnapshot> GetAsync(bool refresh, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(refresh);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(html);
    }

    internal static HttpRequestMessage CreateRequest(bool refresh)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, SourceUrl);
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

    internal static PaygQuotaSnapshot Parse(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var quotaHeading = document.QuerySelector("#quota-tier-reference")
            ?? throw new InvalidOperationException("Microsoft Learn no longer exposes the PAYG quota tier reference in the expected section.");
        var tabGroup = FindNextTabGroup(quotaHeading)
            ?? throw new InvalidOperationException("Microsoft Learn returned no PAYG quota tier tables.");

        var tiers = new List<PaygQuotaTier>();
        foreach (var panel in tabGroup.QuerySelectorAll("[role=tabpanel][data-tab]"))
        {
            var table = panel.QuerySelectorAll("table").FirstOrDefault(IsQuotaTable);
            if (table is null)
            {
                continue;
            }

            var tierName = panel.QuerySelector("h2,h3,h4")?.TextContent.Trim();
            if (string.IsNullOrWhiteSpace(tierName))
            {
                tierName = panel.GetAttribute("data-tab") ?? "Unknown tier";
            }

            var limits = ParseRows(table);
            if (limits.Count == 0)
            {
                throw new InvalidOperationException($"Microsoft Learn returned no PAYG quota rows for {tierName}.");
            }

            tiers.Add(new PaygQuotaTier(tierName, limits));
        }

        if (tiers.Count == 0)
        {
            throw new InvalidOperationException("Microsoft Learn returned no PAYG quota tier tables.");
        }

        return new PaygQuotaSnapshot { Tiers = tiers };
    }

    private static IElement? FindNextTabGroup(IElement heading)
    {
        for (var current = heading.NextElementSibling; current is not null; current = current.NextElementSibling)
        {
            if (current.ClassList.Contains("tabGroup"))
            {
                return current;
            }

            if (current.Matches("h2,h3"))
            {
                break;
            }
        }

        return null;
    }

    private static bool IsQuotaTable(IElement table)
    {
        var headers = table.QuerySelectorAll("thead th")
            .Select(cell => cell.TextContent.Trim())
            .ToArray();

        return headers.Length >= 4
            && string.Equals(headers[0], "Model Name", StringComparison.OrdinalIgnoreCase)
            && string.Equals(headers[1], "Deployment Type", StringComparison.OrdinalIgnoreCase)
            && headers[2].Contains("RPM", StringComparison.OrdinalIgnoreCase)
            && headers[3].Contains("TPM", StringComparison.OrdinalIgnoreCase);
    }

    private static List<PaygQuotaLimit> ParseRows(IElement table)
    {
        var limits = new List<PaygQuotaLimit>();
        foreach (var row in table.QuerySelectorAll("tbody tr"))
        {
            var cells = row.QuerySelectorAll("th,td");
            if (cells.Length < 4 || !TryParseDeploymentType(cells[1].TextContent, out var type))
            {
                continue;
            }

            limits.Add(new PaygQuotaLimit(
                cells[0].TextContent.Trim(),
                type,
                cells[2].TextContent.Trim(),
                cells[3].TextContent.Trim()));
        }

        return limits;
    }

    private static bool TryParseDeploymentType(string value, out PtuType type)
    {
        switch (value.Trim().Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant())
        {
            case "datazonestandard":
                type = PtuType.DataZone;
                return true;
            case "standard":
                type = PtuType.Regional;
                return true;
            case "globalstandard":
                type = PtuType.Global;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
