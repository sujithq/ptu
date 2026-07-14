using Ptu.Cli.Availability;

namespace Ptu.Cli.Tests.Fakes;

public sealed class FakeAvailabilityClient : IAvailabilityClient
{
    public AvailabilitySnapshot Snapshot { get; set; } = CreateSnapshot();

    public Exception? ThrowOnGet { get; set; }

    /// <summary>Endpoint passed to the most recent <see cref="GetAsync"/> call.</summary>
    public string? LastEndpoint { get; private set; }

    /// <summary>Auth cookie passed to the most recent <see cref="GetAsync"/> call.</summary>
    public string? LastAuthCookie { get; private set; }

    public Task<AvailabilitySnapshot> GetAsync(string endpoint, string? authCookie, CancellationToken cancellationToken)
    {
        LastEndpoint = endpoint;
        LastAuthCookie = authCookie;
        return ThrowOnGet is null
            ? Task.FromResult(Snapshot)
            : Task.FromException<AvailabilitySnapshot>(ThrowOnGet);
    }

    public static AvailabilitySnapshot CreateSnapshot(string status = "succeeded") => new()
    {
        Status = status,
        GeneratedAt = new DateTimeOffset(2026, 7, 10, 6, 0, 0, TimeSpan.Zero),
        Regions =
        [
            Region("swedencentral",
                Model("gpt-5.4", dataZone: (true, 100), regional: (false, null), global: (true, 300)),
                Model("gpt-5.4-mini", dataZone: (true, 150), regional: (true, 200), global: (true, 400)),
                Model("gpt-5-mini", dataZone: (false, null), regional: (false, null), global: (true, 500)),
                Model("gpt-4.1", dataZone: (true, 640), regional: (true, 540), global: (true, 870))),
            Region("francecentral",
                Model("gpt-5.4", dataZone: (true, 80), regional: (false, null), global: (false, null)),
                Model("gpt-5.4-mini", dataZone: (false, null), regional: (false, null), global: (true, 350)),
                Model("gpt-5-mini", dataZone: (true, 90), regional: (true, 110), global: (false, null)),
                Model("gpt-4.1", dataZone: (false, null), regional: (true, 220), global: (true, 760))),
        ],
    };

    private static RegionAvailability Region(string name, params ModelAvailability[] models) => new()
    {
        Region = name,
        Models = models,
    };

    private static ModelAvailability Model(
        string name,
        (bool Available, int? Capacity) dataZone,
        (bool Available, int? Capacity) regional,
        (bool Available, int? Capacity) global) => new()
    {
        Name = name,
        Offers = new Dictionary<PtuType, PtuOffer>
        {
            [PtuType.DataZone] = new(dataZone.Available, dataZone.Capacity),
            [PtuType.Regional] = new(regional.Available, regional.Capacity),
            [PtuType.Global] = new(global.Available, global.Capacity),
        },
    };
}
