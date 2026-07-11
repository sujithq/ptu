namespace Ptu.Cli.Availability;

/// <summary>Availability and capacity of one PTU type for a model in a region.</summary>
public readonly record struct PtuOffer(bool Available, int? Capacity);

public sealed class ModelAvailability
{
    public required string Name { get; init; }

    public required IReadOnlyDictionary<PtuType, PtuOffer> Offers { get; init; }
}

public sealed class RegionAvailability
{
    public required string Region { get; init; }

    public required IReadOnlyList<ModelAvailability> Models { get; init; }

    public ModelAvailability? FindModel(string name) =>
        Models.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>The full availability dataset returned by the API.</summary>
public sealed class AvailabilitySnapshot
{
    public required string Status { get; init; }

    public DateTimeOffset? GeneratedAt { get; init; }

    public required IReadOnlyList<RegionAvailability> Regions { get; init; }

    public RegionAvailability? FindRegion(string region) =>
        Regions.FirstOrDefault(r => string.Equals(r.Region, region, StringComparison.OrdinalIgnoreCase));
}
