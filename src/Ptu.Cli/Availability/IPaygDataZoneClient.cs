namespace Ptu.Cli.Availability;

public interface IPaygDataZoneClient
{
    /// <summary>Fetches PAYG Standard availability tables from Microsoft Learn.</summary>
    Task<PaygDataZoneSnapshot> GetAsync(string tab, bool refresh, CancellationToken cancellationToken);
}