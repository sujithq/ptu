using Ptu.Cli.Availability;

namespace Ptu.Cli.Tests.Fakes;

public sealed class FakePaygDataZoneClient : IPaygDataZoneClient
{
    public PaygDataZoneSnapshot Snapshot { get; set; } = CreateSnapshot();

    public Exception? ThrowOnGet { get; set; }

    public int CallCount { get; private set; }

    public bool LastRefresh { get; private set; }

    public string? LastTab { get; private set; }

    public Task<PaygDataZoneSnapshot> GetAsync(string tab, bool refresh, CancellationToken cancellationToken)
    {
        CallCount++;
        LastTab = tab;
        LastRefresh = refresh;
        return ThrowOnGet is null
            ? Task.FromResult(Snapshot)
            : Task.FromException<PaygDataZoneSnapshot>(ThrowOnGet);
    }

    public static PaygDataZoneSnapshot CreateSnapshot() => new()
    {
        Models =
        [
            Model("gpt-5.4", "2026-03-05", "francecentral", "swedencentral"),
            Model("gpt-5.4-mini", "2026-03-17", "francecentral", "swedencentral"),
            Model("gpt-5-mini", "2025-08-07", "francecentral", "swedencentral"),
            Model("gpt-4.1", "2025-04-14", "francecentral", "swedencentral"),
        ],
    };

    public static PaygDataZoneModel Model(string name, string version, params string[] regions) => new()
    {
        Name = name,
        Version = version,
        AvailableRegions = new HashSet<string>(regions, StringComparer.OrdinalIgnoreCase),
    };
}