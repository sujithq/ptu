using Ptu.Cli.Availability;

namespace Ptu.Cli.Tests.Fakes;

public sealed class FakePaygQuotaClient : IPaygQuotaClient
{
    public PaygQuotaSnapshot Snapshot { get; set; } = CreateSnapshot();

    public Exception? ThrowOnGet { get; set; }

    public int CallCount { get; private set; }

    public bool LastRefresh { get; private set; }

    public Task<PaygQuotaSnapshot> GetAsync(bool refresh, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRefresh = refresh;
        return ThrowOnGet is null
            ? Task.FromResult(Snapshot)
            : Task.FromException<PaygQuotaSnapshot>(ThrowOnGet);
    }

    private static PaygQuotaSnapshot CreateSnapshot() => new()
    {
        Tiers =
        [
            new("Tier 1",
            [
                new("gpt-4.1", PtuType.DataZone, "300", "300,000"),
                new("gpt-4.1", PtuType.Global, "1,000", "1,000,000"),
                new("gpt-5.4", PtuType.DataZone, "300", "300,000"),
            ]),
            new("Tier 2",
            [
                new("gpt-4.1", PtuType.DataZone, "670", "670,000"),
                new("gpt-4.1", PtuType.Global, "2,000", "2,000,000"),
                new("gpt-5.4", PtuType.DataZone, "670", "670,000"),
            ]),
        ],
    };
}
