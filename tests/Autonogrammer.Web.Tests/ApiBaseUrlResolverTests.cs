using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>Base-URL resolution: configuration is the default, a query override is honoured only for an allowlisted origin, and absence throws.</summary>
public sealed class ApiBaseUrlResolverTests
{
    private const string Configured = "http://127.0.0.1:5187";
    private const string Page = "https://web.autonogrammer.local/";

    /// <summary>An allowlisted origin in the query override wins over configuration.</summary>
    [Fact]
    public void AllowlistedQueryOverrideWinsOverConfiguration()
    {
        ApiBaseUrlResolver.Resolve(Configured, Page + "?apiBaseUrl=https%3A%2F%2Flocalhost%3A5001", ["https://localhost:5001"])
            .ShouldBe("https://localhost:5001");
    }

    /// <summary>Configuration is used when no override is present, with an empty allowlist: the same-origin default keeps working.</summary>
    [Fact]
    public void ConfigurationIsUsedWithoutOverride()
    {
        ApiBaseUrlResolver.Resolve(Configured, Page, []).ShouldBe(Configured);
    }

    /// <summary>Other query parameters do not interfere.</summary>
    [Fact]
    public void OtherQueryParametersAreIgnored()
    {
        ApiBaseUrlResolver.ExtractOverride("https://web.autonogrammer.local/?session=s-1&theme=dark").ShouldBeNull();
    }

    /// <summary>Missing configuration and override throws naming the key.</summary>
    [Fact]
    public void MissingBaseUrlThrowsNamingTheKey()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(null, Page, []));
        ex.Message.ShouldContain("Api:BaseUrl");
    }

    /// <summary>With an empty allowlist, an override to a foreign origin is refused and configuration is not used in its place.</summary>
    [Fact]
    public void ForeignOriginOverrideIsRefusedWithEmptyAllowlist()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            Configured,
            Page + "?apiBaseUrl=https%3A%2F%2Fevil.example%2Fapi",
            []));
        ex.Message.ShouldContain("apiBaseUrl");
        ex.Message.ShouldContain("Api:OverrideOrigins");
    }

    /// <summary>A loopback origin that is not on the allowlist is cross-origin and is refused, even though it is loopback.</summary>
    [Fact]
    public void LoopbackOverrideNotOnAllowlistIsRefused()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            Configured,
            Page + "?apiBaseUrl=http%3A%2F%2Flocalhost%3A9999",
            ["https://localhost:5001"]));
        ex.Message.ShouldContain("apiBaseUrl");
    }

    /// <summary>A garbage override that is not an allowlisted origin fails fast, naming the parameter.</summary>
    [Fact]
    public void GarbageOverrideThrows()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            Configured,
            Page + "?apiBaseUrl=foo",
            ["https://localhost:5001"]));
        ex.Message.ShouldContain("apiBaseUrl");
    }

    /// <summary>An allowlisted loopback http origin is accepted for local development.</summary>
    [Fact]
    public void AllowlistedLoopbackHttpOverrideIsAccepted()
    {
        ApiBaseUrlResolver.Resolve(null, Page + "?apiBaseUrl=http%3A%2F%2Flocalhost%3A5001", ["http://localhost:5001"])
            .ShouldBe("http://localhost:5001");
    }

    /// <summary>An allowlisted origin keeps the path the override names; the origin decides, not the path.</summary>
    [Fact]
    public void AllowlistedOverrideKeepsItsPath()
    {
        ApiBaseUrlResolver.Resolve(Configured, Page + "?apiBaseUrl=https%3A%2F%2Flocalhost%3A5001%2Fv1", ["https://localhost:5001"])
            .ShouldBe("https://localhost:5001/v1");
    }

    /// <summary>The allowlist match ignores host case and a trailing slash on the entry.</summary>
    [Fact]
    public void AllowlistMatchIgnoresHostCaseAndTrailingSlash()
    {
        ApiBaseUrlResolver.Resolve(Configured, Page + "?apiBaseUrl=https%3A%2F%2Fapi.example.com", ["https://API.example.com/"])
            .ShouldBe("https://api.example.com");
    }

    /// <summary>An allowlisted cleartext http origin outside loopback is refused: the allowlist does not lift the cleartext rule.</summary>
    [Fact]
    public void AllowlistedCleartextNonLoopbackOverrideIsRefused()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            Configured,
            Page + "?apiBaseUrl=http%3A%2F%2Fevil.example",
            ["http://evil.example"]));
        ex.Message.ShouldContain("cleartext");
    }

    /// <summary>Non-http schemes are refused even when the origin text would otherwise match the allowlist.</summary>
    [Theory]
    [InlineData("ftp%3A%2F%2Flocalhost%3A5001")]
    [InlineData("javascript%3Aalert(1)")]
    [InlineData("file%3A%2F%2F%2Fetc%2Fpasswd")]
    public void NonHttpSchemeOverrideIsRefused(string encodedOverride)
    {
        Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            Configured,
            Page + "?apiBaseUrl=" + encodedOverride,
            ["https://localhost:5001"]));
    }

    /// <summary>An allowlist entry that carries a path is not an origin and fails the boot, naming the key, even with no override in the page.</summary>
    [Fact]
    public void AllowlistEntryWithPathFailsTheBootWithoutOverride()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(Configured, Page, ["https://localhost:5001/api"]));
        ex.Message.ShouldContain("Api:OverrideOrigins");
    }

    /// <summary>A configured non-loopback https value is accepted: the allowlist governs only the query override.</summary>
    [Fact]
    public void NonLoopbackHttpsConfigurationIsAccepted()
    {
        ApiBaseUrlResolver.Resolve("https://api.example.com/v1", Page, []).ShouldBe("https://api.example.com/v1");
    }

    /// <summary>A configured cleartext http value outside loopback is refused.</summary>
    [Fact]
    public void PlainHttpConfigurationToNonLoopbackHostIsRejected()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve("http://evil.example/api", Page, []));
        ex.Message.ShouldContain("cleartext");
    }

    /// <summary>An http URL to 127.0.0.1 or localhost is accepted by validation.</summary>
    [Fact]
    public void LoopbackHttpVariantsAreAccepted()
    {
        ApiBaseUrlResolver.Validate("http://127.0.0.1:5187", false).ShouldBe("http://127.0.0.1:5187");
        ApiBaseUrlResolver.Validate("http://localhost:5001", false).ShouldBe("http://localhost:5001");
    }
}
