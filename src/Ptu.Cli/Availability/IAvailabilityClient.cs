namespace Ptu.Cli.Availability;

public interface IAvailabilityClient
{
    /// <summary>Fetches the full PTU availability dataset from the given endpoint. The API does not support server-side filtering.</summary>
    Task<AvailabilitySnapshot> GetAsync(string endpoint, CancellationToken cancellationToken);
}
