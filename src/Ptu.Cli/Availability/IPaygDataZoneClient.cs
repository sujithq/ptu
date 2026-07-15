namespace Ptu.Cli.Availability;

public interface IPaygDataZoneClient
{
    /// <summary>Fetches PAYG Data Zone Standard model availability from Microsoft Learn.</summary>
    Task<PaygDataZoneSnapshot> GetAsync(string tab, bool refresh, CancellationToken cancellationToken);
}