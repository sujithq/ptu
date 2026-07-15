using Ptu.Cli.Availability;

namespace Ptu.Cli.Tests;

public class HttpPaygDataZoneClientTests
{
    [Fact]
    public void Parse_ReadsOnlyAzureOpenAiDataZoneStandardTablesAndAggregatesVersions()
    {
        const string html = """
            <div><h2 id="global-standard">Global Standard</h2></div>
            <div><h4>Availability for Azure OpenAI in Foundry Models</h4></div>
            <div><table><thead><tr><th>Model</th><th>Version</th><th>swedencentral</th></tr></thead>
            <tbody><tr><td>global-only</td><td>1</td><td>&#x2705;</td></tr></tbody></table></div>
            <div><h2 id="data-zone-standard">Data Zone Standard</h2></div>
            <p>PAYG Data Zone deployment availability.</p>
            <div><h4>Availability for Azure OpenAI in Foundry Models</h4></div>
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
            <div><h2 id="standardregional">Standard/Regional</h2></div>
            """;

        var snapshot = HttpPaygDataZoneClient.Parse(html);

        Assert.Equal(2, snapshot.Models.Count);
        Assert.True(snapshot.IsAvailable("GPT-4.1", "swedencentral"));
        Assert.True(snapshot.IsAvailable("gpt-4.1", "FranceCentral"));
        Assert.False(snapshot.IsAvailable("gpt-4.1", "norwayeast"));
        Assert.False(snapshot.IsAvailable("global-only", "swedencentral"));
        Assert.False(snapshot.IsAvailable("other-model", "francecentral"));
    }

    [Fact]
    public void Parse_WithoutExpectedDataZoneSection_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            HttpPaygDataZoneClient.Parse("<html><body><h2>Unavailable</h2></body></html>"));

        Assert.Contains("expected section", exception.Message);
    }

    [Fact]
    public void Parse_WithGeographyPanels_ReadsOnlySelectedTab()
    {
        const string html = """
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
                        """;

        var snapshot = HttpPaygDataZoneClient.Parse(html, PaygDataZoneTabs.Europe);

        Assert.True(snapshot.IsAvailable("gpt-4.1", "swedencentral"));
        Assert.False(snapshot.IsAvailable("gpt-4.1", "eastus"));
    }

    [Fact]
    public void Parse_WithGpt51GeographyPanels_IsolatesTabsAndAcceptsUnavailablePanel()
    {
        const string html = """
            <h2 id="data-zone-standard">Data Zone Standard</h2>
            <h4>Availability for Azure OpenAI in Foundry Models</h4>
            <div class="tabGroup">
                <section role="tabpanel" data-tab="az-americas">
                    <table><thead><tr><th>Model</th><th>Version</th><th>centralus</th><th>eastus</th></tr></thead>
                    <tbody><tr><td>gpt-5.1</td><td>2025-11-13</td><td>&#x2705;</td><td>&#x2705;</td></tr></tbody></table>
                </section>
                <section role="tabpanel" data-tab="az-europe">
                    <table><thead><tr><th>Model</th><th>Version</th><th>francecentral</th><th>swedencentral</th></tr></thead>
                    <tbody><tr><td>gpt-5.1</td><td>2025-11-13</td><td>&#x2705;</td><td>&#x2705;</td></tr></tbody></table>
                </section>
                <section role="tabpanel" data-tab="az-apac">
                    <table><thead><tr><th>Model</th><th>Version</th><th>australiaeast</th></tr></thead>
                    <tbody><tr><td>gpt-5.2</td><td>2025-12-11</td><td>&#x2705;</td></tr></tbody></table>
                </section>
                <section role="tabpanel" data-tab="az-mea"><p>Not available</p></section>
            </div>
            <h2 id="standardregional">Standard/Regional</h2>
            """;

        var americas = HttpPaygDataZoneClient.Parse(html, PaygDataZoneTabs.Americas);
        var europe = HttpPaygDataZoneClient.Parse(html, PaygDataZoneTabs.Europe);
        var asiaPacific = HttpPaygDataZoneClient.Parse(html, PaygDataZoneTabs.AsiaPacific);
        var middleEastAfrica = HttpPaygDataZoneClient.Parse(html, PaygDataZoneTabs.MiddleEastAfrica);

        Assert.True(americas.IsAvailable("gpt-5.1", "eastus"));
        Assert.False(americas.IsAvailable("gpt-5.1", "swedencentral"));
        Assert.True(europe.IsAvailable("gpt-5.1", "swedencentral"));
        Assert.False(europe.IsAvailable("gpt-5.1", "eastus"));
        Assert.False(asiaPacific.IsAvailable("gpt-5.1", "australiaeast"));
        Assert.Empty(middleEastAfrica.Models);
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