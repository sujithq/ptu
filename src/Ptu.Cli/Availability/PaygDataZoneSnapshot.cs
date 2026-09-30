namespace Ptu.Cli.Availability;

public sealed class PaygDataZoneModel
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public required IReadOnlySet<string> AvailableRegions { get; init; }
}

/// <summary>PAYG Standard availability by deployment type, published by Microsoft Learn.</summary>
public sealed class PaygDataZoneSnapshot
{
    public required IReadOnlyDictionary<PtuType, IReadOnlyList<PaygDataZoneModel>> ModelsByType { get; init; }

    public bool IsAvailable(PtuType type, string model, string region) =>
        ModelsByType.TryGetValue(type, out var models)
        && models.Any(item =>
            string.Equals(item.Name, model, StringComparison.OrdinalIgnoreCase)
            && item.AvailableRegions.Contains(region));
}