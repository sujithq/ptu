using System.Net.Http.Json;
using System.Text.Json;

namespace Ptu.Cli.Availability;

/// <summary>Queries an azure-ptu availability endpoint.</summary>
public sealed class HttpAvailabilityClient(HttpClient http) : IAvailabilityClient
{
    /// <summary>The API sits behind a browser-oriented gateway; a browser user agent keeps it happy.</summary>
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AvailabilitySnapshot> GetAsync(
        string endpoint,
        string? authCookie,
        bool refresh,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(endpoint, authCookie, refresh);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ApiResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The availability API returned an empty response.");

        return Map(dto);
    }

    /// <summary>Builds a browser-like GET request; requires the HttpClient handler to have UseCookies disabled.</summary>
    internal static HttpRequestMessage CreateRequest(string endpoint, string? authCookie, bool refresh)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");

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

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            request.Headers.TryAddWithoutValidation("Referer", $"{uri.Scheme}://{uri.Authority}/availability");
        }

        if (!string.IsNullOrWhiteSpace(authCookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", authCookie);
        }

        return request;
    }

    private static AvailabilitySnapshot Map(ApiResponse dto) => new()
    {
        Status = dto.Status ?? "unknown",
        GeneratedAt = dto.GeneratedAt,
        Regions = [.. (dto.Payload?.Regions ?? [])
            .Where(r => !string.IsNullOrEmpty(r.Region))
            .Select(r => new RegionAvailability
            {
                Region = r.Region!,
                Models = [.. (r.Models ?? [])
                    .Where(m => !string.IsNullOrEmpty(m.Name))
                    .Select(m => new ModelAvailability
                    {
                        Name = m.Name!,
                        Offers = new Dictionary<PtuType, PtuOffer>
                        {
                            [PtuType.DataZone] = new(m.DataZoneProvisionedAvailable ?? false, m.DataZoneProvisionedCapacity),
                            [PtuType.Regional] = new(m.ProvisionedAvailable ?? false, m.ProvisionedCapacity),
                            [PtuType.Global] = new(m.GlobalProvisionedAvailable ?? false, m.GlobalProvisionedCapacity),
                        },
                    })],
            })],
    };

    private sealed class ApiResponse
    {
        public string? Status { get; set; }

        public DateTimeOffset? GeneratedAt { get; set; }

        public ApiPayload? Payload { get; set; }
    }

    private sealed class ApiPayload
    {
        public List<ApiRegion>? Regions { get; set; }
    }

    private sealed class ApiRegion
    {
        public string? Region { get; set; }

        public List<ApiModel>? Models { get; set; }
    }

    private sealed class ApiModel
    {
        public string? Name { get; set; }

        public bool? ProvisionedAvailable { get; set; }

        public int? ProvisionedCapacity { get; set; }

        public bool? DataZoneProvisionedAvailable { get; set; }

        public int? DataZoneProvisionedCapacity { get; set; }

        public bool? GlobalProvisionedAvailable { get; set; }

        public int? GlobalProvisionedCapacity { get; set; }
    }
}
