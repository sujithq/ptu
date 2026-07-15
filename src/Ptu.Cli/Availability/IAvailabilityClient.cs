namespace Ptu.Cli.Availability;

public interface IAvailabilityClient
{
    /// <summary>
    /// Fetches the full PTU availability dataset from the given endpoint. The API does not support server-side
    /// filtering. <paramref name="authCookie"/> is an optional session cookie ("name=value") for secured endpoints.
    /// <paramref name="refresh"/> requests cache revalidation for a fresh snapshot.
    /// </summary>
    Task<AvailabilitySnapshot> GetAsync(string endpoint, string? authCookie, bool refresh, CancellationToken cancellationToken);
}
