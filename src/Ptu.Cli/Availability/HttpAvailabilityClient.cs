using System.Net.Http.Json;
using System.Text.Json;

namespace Ptu.Cli.Availability;

/// <summary>Queries an azure-ptu availability endpoint.</summary>
public sealed class HttpAvailabilityClient(HttpClient http) : IAvailabilityClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AvailabilitySnapshot> GetAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ApiResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The availability API returned an empty response.");

        return Map(dto);
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
