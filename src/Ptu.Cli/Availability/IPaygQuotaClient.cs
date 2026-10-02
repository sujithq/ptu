namespace Ptu.Cli.Availability;

public interface IPaygQuotaClient
{
    /// <summary>Fetches PAYG Standard RPM and TPM limits by quota tier from Microsoft Learn.</summary>
    Task<PaygQuotaSnapshot> GetAsync(bool refresh, CancellationToken cancellationToken);
}
