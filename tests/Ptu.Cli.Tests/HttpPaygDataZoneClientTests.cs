using Ptu.Cli.Availability;

namespace Ptu.Cli.Tests;

public class HttpPaygDataZoneClientTests
{
    [Fact]
    public void Parse_ReadsAllAzureOpenAiStandardSectionsAndAggregatesVersions()
    {
        const string html = """
            <h2 id="global-standard">Global Standard</h2>
            <p>Global availability.</p>
            <h4>Availability for Azure OpenAI in Foundry Models</h4>
            <div><table><thead><tr><th>Model</th><th>Version</th><th>swedencentral</th></tr></thead>
            <tbody><tr><td>global-only</td><td>1</td><td>&#x2705;</td></tr></tbody></table></div>
            <h2 id="data-zone-standard">Data Zone Standard</h2>
            <p>Data Zone availability.</p>
            <h4>Availability for Azure OpenAI in Foundry Models</h4>
            <div class="tabGroup">
              <table><thead><tr><th>Model</th><th>Version</th><th>francecentral</th><th>swedencentral</th></tr></thead>
              <tbody>
                <tr><td>gpt-4.1</td><td>2025-04-14</td><td>-</td><td>&#x2705;</td></tr>
                <tr><td>gpt-4.1</td><td>2026-01-01</td><td>&#x2705;</td><td>-</td></tr>
              </tbody></table>
            </div>
            <div><h4>Availability for other Foundry Models sold by Azure</h4></div>
            <div><table><thead><tr><th>Model</th><th>Version</th><th>francecentral</th></tr></thead>
            <tbody><tr><td>other-model</td><td>1</td><td>&#x2705;</td></tr></tbody></table></div>
            <h2 id="standardregional">Standard/Regional</h2>
            <p>Regional availability.</p>
            <h4>Availability for Azure OpenAI in Foundry Models</h4>
            <div><table><thead><tr><th>Model</th><th>Version</th><th>francecentral</th></tr></thead>
            <tbody><tr><td>gpt-4.1</td><td>2025-04-14</td><td>&#x2705;</td></tr></tbody></table></div>
            """;

        var snapshot = HttpPaygDataZoneClient.Parse(html);

        Assert.Equal(2, snapshot.ModelsByType[PtuType.DataZone].Count);
        Assert.True(snapshot.IsAvailable(PtuType.Global, "GLOBAL-ONLY", "swedencentral"));
        Assert.True(snapshot.IsAvailable(PtuType.DataZone, "GPT-4.1", "swedencentral"));
        Assert.True(snapshot.IsAvailable(PtuType.DataZone, "gpt-4.1", "FranceCentral"));
        Assert.False(snapshot.IsAvailable(PtuType.DataZone, "gpt-4.1", "norwayeast"));
        Assert.False(snapshot.IsAvailable(PtuType.Global, "gpt-4.1", "swedencentral"));
        Assert.True(snapshot.IsAvailable(PtuType.Regional, "gpt-4.1", "francecentral"));
        Assert.False(snapshot.IsAvailable(PtuType.Regional, "other-model", "francecentral"));
    }

    [Fact]
    public void Parse_WithoutExpectedStandardSection_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            HttpPaygDataZoneClient.Parse("<html><body><h2>Unavailable</h2></body></html>"));

        Assert.Contains("expected section", exception.Message);
    }

    [Fact]
    public void Parse_WithGeographyPanels_ReadsOnlySelectedTab()
    {
        const string html = """
            <h2 id="global-standard">Global Standard</h2>
            <h4>Availability for Azure OpenAI in Foundry Models</h4>
            <div><table><thead><tr><th>Model</th><th>Version</th><th>swedencentral</th></tr></thead>
            <tbody><tr><td>gpt-4.1</td><td>1</td><td>&#x2705;</td></tr></tbody></table></div>
            <h2 id="data-zone-standard">Data Zone Standard</h2>
            <h4>Availability for Azure OpenAI in Foundry Models</h4>
            <div class="tabGroup">
                <section role="tabpanel" data-tab="az-americas">
                    <table><thead><tr><th>Model</th><th>Version</th><th>eastus</th></tr></thead>
                    <tbody><tr><td>gpt-4.1</td><td>1</td><td>&#x2705;</td></tr></tbody></table>
                </section>
                <section role="tabpanel" data-tab="az-europe">
                    <table><thead><tr><th>Model</th><th>Version</th><th>swedencentral</th></tr></thead>
                    <tbody><tr><td>gpt-4.1</td><td>1</td><td>&#x2705;</td></tr></tbody></table>
                </section>
            </div>
            <h2 id="standardregional">Standard/Regional</h2>
            <h4>Availability for Azure OpenAI in Foundry Models</h4>
            <div><table><thead><tr><th>Model</th><th>Version</th><th>francecentral</th></tr></thead>
            <tbody><tr><td>gpt-4.1</td><td>1</td><td>&#x2705;</td></tr></tbody></table></div>
            """;

        var snapshot = HttpPaygDataZoneClient.Parse(html, PaygDataZoneTabs.Europe);

        Assert.True(snapshot.IsAvailable(PtuType.DataZone, "gpt-4.1", "swedencentral"));
        Assert.False(snapshot.IsAvailable(PtuType.DataZone, "gpt-4.1", "eastus"));
        Assert.True(snapshot.IsAvailable(PtuType.Global, "gpt-4.1", "swedencentral"));
        Assert.True(snapshot.IsAvailable(PtuType.Regional, "gpt-4.1", "francecentral"));
    }

    [Theory]
    [InlineData(PaygDataZoneTabs.Americas)]
    [InlineData(PaygDataZoneTabs.Europe)]
    [InlineData(PaygDataZoneTabs.AsiaPacific)]
    [InlineData(PaygDataZoneTabs.MiddleEastAfrica)]
    public void CreateRequest_WithSupportedTab_IncludesTabInQuery(string tab)
    {
        using var request = HttpPaygDataZoneClient.CreateRequest(tab, refresh: false);

        Assert.Equal($"{HttpPaygDataZoneClient.SourceUrl}&tabs={tab}", request.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public void CreateRequest_WithRefresh_BypassesCaches()
    {
        using var request = HttpPaygDataZoneClient.CreateRequest(PaygDataZoneTabs.Europe, refresh: true);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{HttpPaygDataZoneClient.SourceUrl}&tabs=az-europe", request.RequestUri?.AbsoluteUri);
        Assert.True(request.Headers.CacheControl?.NoCache);
        Assert.True(request.Headers.CacheControl?.NoStore);
        Assert.Equal(TimeSpan.Zero, request.Headers.CacheControl?.MaxAge);
        Assert.Contains("no-cache", request.Headers.GetValues("Pragma"));
    }
}
