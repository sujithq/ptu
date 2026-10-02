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

    /// <summary>Batch availability by deployment type. Microsoft Learn documents Global Batch and Data Zone Batch only.</summary>
    public IReadOnlyDictionary<PtuType, IReadOnlyList<PaygDataZoneModel>> BatchModelsByType { get; init; } =
        new Dictionary<PtuType, IReadOnlyList<PaygDataZoneModel>>();

    /// <summary>True when Microsoft Learn documents a Batch deployment option for <paramref name="type"/>.</summary>
    public static bool SupportsBatch(PtuType type) => type is PtuType.Global or PtuType.DataZone;

    public bool IsAvailable(PtuType type, string model, string region) =>
        IsAvailable(ModelsByType, type, model, region);

    /// <summary>
    /// Batch availability for <paramref name="type"/>, or <see langword="null"/> when Microsoft Learn
    /// published no parseable Batch section for that deployment type.
    /// </summary>
    public bool? GetBatchAvailability(PtuType type, string model, string region) =>
        SupportsBatch(type) && BatchModelsByType.ContainsKey(type)
            ? IsAvailable(BatchModelsByType, type, model, region)
            : null;

    private static bool IsAvailable(
        IReadOnlyDictionary<PtuType, IReadOnlyList<PaygDataZoneModel>> source,
        PtuType type,
        string model,
        string region) =>
        source.TryGetValue(type, out var models)
        && models.Any(item =>
            string.Equals(item.Name, model, StringComparison.OrdinalIgnoreCase)
            && item.AvailableRegions.Contains(region));
}