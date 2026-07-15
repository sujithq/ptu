namespace Ptu.Cli.Availability;

public sealed class PaygDataZoneModel
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public required IReadOnlySet<string> AvailableRegions { get; init; }
}

/// <summary>PAYG Data Zone Standard availability published by Microsoft Learn.</summary>
public sealed class PaygDataZoneSnapshot
{
    public required IReadOnlyList<PaygDataZoneModel> Models { get; init; }

    public bool IsAvailable(string model, string region) =>
        Models.Any(item =>
            string.Equals(item.Name, model, StringComparison.OrdinalIgnoreCase)
            && item.AvailableRegions.Contains(region));
}