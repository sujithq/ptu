using System.Net;
using System.Text;
using Ptu.Cli.Availability;
using Ptu.Cli.Configuration;

namespace Ptu.Cli.Tests;

public class AuthTests
{
    private const string Cookie = "session_cookie=abc123";

    /// <summary>Builds a cookie whose value is URL-encoded base64 JSON, as issued by the API's login flow.</summary>
    private static string DecodableCookie(string username, DateTimeOffset expiresAt)
    {
        var json = $$"""{"token":"t","username":"{{username}}","expiry":{{expiresAt.ToUnixTimeMilliseconds()}}}""";
        return "session_cookie=" + Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void AuthSet_StoresCookie()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("auth", "set", Cookie);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(Cookie, store.Config.AuthCookie);
        Assert.Contains("session_cookie", result.Output);
    }

    [Fact]
    public void AuthSet_WithDecodableCookie_PrintsExpiry()
    {
        var (app, _, _) = TestHost.Create();
        var cookie = DecodableCookie("user@example.com", new DateTimeOffset(2027, 1, 15, 10, 31, 25, TimeSpan.Zero));

        var result = app.Run("auth", "set", cookie);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("2027-01-15 10:31:25Z", result.Output);
    }

    [Fact]
    public void AuthSet_WithExpiredCookie_Warns()
    {
        var (app, store, _) = TestHost.Create();
        var cookie = DecodableCookie("user@example.com", DateTimeOffset.UtcNow.AddDays(-1));

        var result = app.Run("auth", "set", cookie);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("expired", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(cookie, store.Config.AuthCookie);
    }

    [Theory]
    [InlineData("no-separator")]
    [InlineData("=value-only")]
    [InlineData("name-only=")]
    public void AuthSet_WithMalformedPair_FailsWithExitCode1(string cookie)
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("auth", "set", cookie);

        Assert.Equal(1, result.ExitCode);
        Assert.Null(store.Config.AuthCookie);
    }

    [Fact]
    public void AuthShow_WhenNotConfigured_SaysNotConfigured()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("auth", "show");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("not configured", result.Output);
    }

    [Fact]
    public void AuthShow_NeverPrintsTheCookieValue()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.AuthCookie = Cookie;

        var result = app.Run("auth", "show");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("configured", result.Output);
        Assert.Contains("session_cookie", result.Output);
        Assert.DoesNotContain("abc123", result.Output);
    }

    [Fact]
    public void AuthShow_WithDecodableCookie_PrintsUsernameAndExpiry()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.AuthCookie = DecodableCookie("user@example.com", new DateTimeOffset(2027, 1, 15, 10, 31, 25, TimeSpan.Zero));

        var result = app.Run("auth", "show");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("user@example.com", result.Output);
        Assert.Contains("2027-01-15 10:31:25Z", result.Output);
    }

    [Fact]
    public void AuthShow_WithExpiredCookie_MarksItExpired()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.AuthCookie = DecodableCookie("user@example.com", DateTimeOffset.UtcNow.AddDays(-1));

        var result = app.Run("auth", "show");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("expired", result.Output);
    }

    [Fact]
    public void AuthClear_RemovesStoredCookie()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.AuthCookie = Cookie;

        var result = app.Run("auth", "clear");

        Assert.Equal(0, result.ExitCode);
        Assert.Null(store.Config.AuthCookie);
    }

    [Fact]
    public void AuthClear_WhenNotConfigured_StillSucceeds()
    {
        var (app, store, _) = TestHost.Create();

        var result = app.Run("auth", "clear");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void Availability_PassesStoredCookieToClient()
    {
        var (app, store, client) = TestHost.Create();
        store.Config.AuthCookie = Cookie;

        var result = app.Run("availability");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(Cookie, client.LastAuthCookie);
    }

    [Fact]
    public void Availability_WithoutCookie_PassesNullToClient()
    {
        var (app, _, client) = TestHost.Create();

        var result = app.Run("availability");

        Assert.Equal(0, result.ExitCode);
        Assert.Null(client.LastAuthCookie);
    }

    [Fact]
    public void Availability_OnUnauthorized_WithoutCookie_ExplainsHowToAuthenticate()
    {
        var (app, _, client) = TestHost.Create();
        client.ThrowOnGet = new HttpRequestException("401", null, HttpStatusCode.Unauthorized);

        var result = app.Run("availability");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("requires authentication", result.Output);
        Assert.Contains("ptu auth set", result.Output);
    }

    [Fact]
    public void Availability_OnForbidden_WithStoredCookie_SuggestsRefreshingIt()
    {
        var (app, store, client) = TestHost.Create();
        store.Config.AuthCookie = Cookie;
        client.ThrowOnGet = new HttpRequestException("403", null, HttpStatusCode.Forbidden);

        var result = app.Run("availability");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("expired", result.Output);
        Assert.Contains("ptu auth set", result.Output);
    }

    [Fact]
    public void PresetResetAll_PreservesAuthCookie()
    {
        var (app, store, _) = TestHost.Create();
        store.Config.AuthCookie = Cookie;

        var result = app.Run("preset", "reset", "--all");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(Cookie, store.Config.AuthCookie);
    }

    [Fact]
    public void CreateRequest_SendsBrowserHeadersAndCookie()
    {
        using var request = HttpAvailabilityClient.CreateRequest("https://unit.test/api/availability/azure-ptu", Cookie, false);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("Mozilla/5.0", string.Join(" ", request.Headers.GetValues("User-Agent")));
        Assert.Equal("*/*", string.Join(",", request.Headers.GetValues("Accept")));
        Assert.Equal("https://unit.test/availability", string.Join(",", request.Headers.GetValues("Referer")));
        Assert.Equal(Cookie, string.Join(";", request.Headers.GetValues("Cookie")));
    }

    [Fact]
    public void CreateRequest_WithoutCookie_OmitsCookieHeader()
    {
        using var request = HttpAvailabilityClient.CreateRequest("https://unit.test/api", null, false);

        Assert.False(request.Headers.Contains("Cookie"));
    }

    [Fact]
    public void CreateRequest_WithRefresh_BypassesCaches()
    {
        using var request = HttpAvailabilityClient.CreateRequest("https://unit.test/api", null, true);

        Assert.True(request.Headers.CacheControl?.NoCache);
        Assert.True(request.Headers.CacheControl?.NoStore);
        Assert.Equal(TimeSpan.Zero, request.Headers.CacheControl?.MaxAge);
        Assert.Equal("no-cache", string.Join(",", request.Headers.GetValues("Pragma")));
    }

    [Fact]
    public void AuthCookie_TryDecodeMetadata_WithOpaqueValue_ReturnsNull()
    {
        Assert.Null(AuthCookie.TryDecodeMetadata("session_cookie=not-base64!"));
    }
}
