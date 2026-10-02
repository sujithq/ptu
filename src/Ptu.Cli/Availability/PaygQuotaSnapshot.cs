namespace Ptu.Cli.Availability;

public sealed record PaygQuotaLimit(
    string Model,
    PtuType Type,
    string RequestsPerMinute,
    string TokensPerMinute);

public sealed record PaygQuotaTier(string Name, IReadOnlyList<PaygQuotaLimit> Limits);

/// <summary>PAYG Standard RPM and TPM limits by quota tier, published by Microsoft Learn.</summary>
public sealed class PaygQuotaSnapshot
{
    public required IReadOnlyList<PaygQuotaTier> Tiers { get; init; }
}
