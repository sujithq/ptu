using Ptu.Cli.Availability;

namespace Ptu.Cli.Tests;

public class HttpPaygQuotaClientTests
{
    [Fact]
    public void Parse_ReadsEachTierAndPreservesPublishedRateValues()
    {
        const string html = """
            <h3 id="quota-tier-reference">Quota tier reference</h3>
            <p>Quota details.</p>
            <div class="tabGroup">
              <section role="tabpanel" data-tab="tier1">
                <h3>Tier 1</h3>
                <table><thead><tr>
                  <th>Model Name</th><th>Deployment Type</th>
                  <th>Requests Per Minute (RPM)</th><th>Tokens Per Minute (TPM)</th>
                </tr></thead><tbody>
                  <tr><td>gpt-4.1</td><td>DataZoneStandard</td><td>300</td><td>300,000</td></tr>
                  <tr><td>gpt-4o</td><td>GlobalStandard</td><td>300 / 10s</td><td>300,000</td></tr>
                  <tr><td>gpt-image-1</td><td>GlobalStandard</td><td>9</td><td>-</td></tr>
                </tbody></table>
              </section>
              <section role="tabpanel" data-tab="tier2">
                <h3>Tier 2</h3>
                <table><thead><tr>
                  <th>Model Name</th><th>Deployment Type</th>
                  <th>Requests Per Minute (RPM)</th><th>Tokens Per Minute (TPM)</th>
                </tr></thead><tbody>
                  <tr><td>gpt-4.1</td><td>Standard</td><td>600</td><td>600,000</td></tr>
                </tbody></table>
              </section>
            </div>
            """;

        var snapshot = HttpPaygQuotaClient.Parse(html);

        Assert.Equal(2, snapshot.Tiers.Count);
        Assert.Equal("Tier 1", snapshot.Tiers[0].Name);
        Assert.Collection(
            snapshot.Tiers[0].Limits,
            limit =>
            {
                Assert.Equal("gpt-4.1", limit.Model);
                Assert.Equal(PtuType.DataZone, limit.Type);
                Assert.Equal("300", limit.RequestsPerMinute);
                Assert.Equal("300,000", limit.TokensPerMinute);
            },
            limit =>
            {
                Assert.Equal(PtuType.Global, limit.Type);
                Assert.Equal("300 / 10s", limit.RequestsPerMinute);
            },
            limit =>
            {
                Assert.Equal("gpt-image-1", limit.Model);
                Assert.Equal("-", limit.TokensPerMinute);
            });
        Assert.Equal(PtuType.Regional, Assert.Single(snapshot.Tiers[1].Limits).Type);
    }

    [Fact]
    public void Parse_IgnoresUnrecognizedDeploymentTypes()
    {
        const string html = """
            <h3 id="quota-tier-reference">Quota tier reference</h3>
            <div class="tabGroup">
              <section role="tabpanel" data-tab="tier1">
                <h3>Tier 1</h3>
                <table><thead><tr>
                  <th>Model Name</th><th>Deployment Type</th>
                  <th>Requests Per Minute (RPM)</th><th>Tokens Per Minute (TPM)</th>
                </tr></thead><tbody>
                  <tr><td>gpt-4.1</td><td>Batch</td><td>300</td><td>300,000</td></tr>
                  <tr><td>gpt-4.1</td><td>GlobalStandard</td><td>1,000</td><td>1,000,000</td></tr>
                </tbody></table>
              </section>
            </div>
            """;

        var limit = Assert.Single(Assert.Single(HttpPaygQuotaClient.Parse(html).Tiers).Limits);

        Assert.Equal(PtuType.Global, limit.Type);
    }

    [Fact]
    public void Parse_WithoutQuotaSection_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            HttpPaygQuotaClient.Parse("<html><body><h2>Unavailable</h2></body></html>"));

        Assert.Contains("expected section", exception.Message);
    }

    [Fact]
    public void CreateRequest_WithRefresh_BypassesCaches()
    {
        using var request = HttpPaygQuotaClient.CreateRequest(refresh: true);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(HttpPaygQuotaClient.SourceUrl, request.RequestUri?.AbsoluteUri);
        Assert.True(request.Headers.CacheControl?.NoCache);
        Assert.True(request.Headers.CacheControl?.NoStore);
        Assert.Equal(TimeSpan.Zero, request.Headers.CacheControl?.MaxAge);
        Assert.Contains("no-cache", request.Headers.GetValues("Pragma"));
    }
}
