using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>Base-URL resolution: query override wins, config is the default, absence throws.</summary>
public sealed class ApiBaseUrlResolverTests
{
    /// <summary>The query override wins over configuration.</summary>
    [Fact]
    public void QueryOverrideWinsOverConfiguration()
    {
        ApiBaseUrlResolver.Resolve("http://127.0.0.1:5187", "https://web.ompos.local/?apiBaseUrl=https%3A%2F%2Flocalhost%3A5001")
            .ShouldBe("https://localhost:5001");
    }

    /// <summary>Configuration is used when no override is present.</summary>
    [Fact]
    public void ConfigurationIsUsedWithoutOverride()
    {
        ApiBaseUrlResolver.Resolve("http://127.0.0.1:5187", "https://web.ompos.local/").ShouldBe("http://127.0.0.1:5187");
    }

    /// <summary>Other query parameters do not interfere.</summary>
    [Fact]
    public void OtherQueryParametersAreIgnored()
    {
        ApiBaseUrlResolver.ExtractOverride("https://web.ompos.local/?session=s-1&theme=dark").ShouldBeNull();
    }

    /// <summary>Missing configuration and override throws naming the key.</summary>
    [Fact]
    public void MissingBaseUrlThrowsNamingTheKey()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(null, "https://web.ompos.local/"));
        ex.Message.ShouldContain("Api:BaseUrl");
    }

    /// <summary>A garbage override fails fast instead of crashing URI construction later.</summary>
    [Fact]
    public void GarbageOverrideThrows()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            "http://127.0.0.1:5187",
            "https://web.ompos.local/?apiBaseUrl=foo"));
        ex.Message.ShouldContain("apiBaseUrl");
    }

    /// <summary>Cleartext http outside loopback is rejected.</summary>
    [Fact]
    public void NonLoopbackHttpThrows()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            "http://127.0.0.1:5187",
            "https://web.ompos.local/?apiBaseUrl=http%3A%2F%2Fevil.example%2Fapi"));
        ex.Message.ShouldContain("apiBaseUrl");
    }

    /// <summary>Loopback http stays valid for local development.</summary>
    [Fact]
    public void LoopbackHttpIsAccepted()
    {
        ApiBaseUrlResolver.Resolve("http://127.0.0.1:5187", "https://web.ompos.local/")
            .ShouldBe("http://127.0.0.1:5187");
        ApiBaseUrlResolver.Resolve(null, "https://web.ompos.local/?apiBaseUrl=http%3A%2F%2Flocalhost%3A5001")
            .ShouldBe("http://localhost:5001");
    }

    /// <summary>An ftp override is rejected.</summary>
    [Fact]
    public void FtpSchemeOverrideIsRejected()
    {
        Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            "http://127.0.0.1:5187",
            "https://web.ompos.local/?apiBaseUrl=ftp%3A%2F%2Fevil.example%2Fapi"));
    }

    /// <summary>A javascript override is rejected.</summary>
    [Fact]
    public void JavaScriptSchemeOverrideIsRejected()
    {
        Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            "http://127.0.0.1:5187",
            "https://web.ompos.local/?apiBaseUrl=javascript%3Aalert(1)"));
    }

    /// <summary>A file override is rejected.</summary>
    [Fact]
    public void FileSchemeOverrideIsRejected()
    {
        Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            "http://127.0.0.1:5187",
            "https://web.ompos.local/?apiBaseUrl=file%3A%2F%2F%2Fetc%2Fpasswd"));
    }

    /// <summary>A plain http URL to a non-loopback host is rejected.</summary>
    [Fact]
    public void PlainHttpToNonLoopbackHostIsRejected()
    {
        Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            null,
            "https://web.ompos.local/?apiBaseUrl=http%3A%2F%2Fevil.example"));
    }

    /// <summary>A loopback https override is accepted.</summary>
    [Fact]
    public void LoopbackHttpsOverrideIsAccepted()
    {
        ApiBaseUrlResolver.Resolve(
                "http://127.0.0.1:5187",
                "https://web.ompos.local/?apiBaseUrl=https%3A%2F%2Flocalhost%3A5001%2Fv1")
            .ShouldBe("https://localhost:5001/v1");
    }

    /// <summary>A non-loopback https override is refused, and the error names the query parameter.</summary>
    [Fact]
    public void NonLoopbackHttpsOverrideThrowsNamingTheParameter()
    {
        var ex = Should.Throw<InvalidOperationException>(() => ApiBaseUrlResolver.Resolve(
            "http://127.0.0.1:5187",
            "https://web.ompos.local/?apiBaseUrl=https%3A%2F%2Fapi.example.com%2Fv1"));
        ex.Message.ShouldContain("apiBaseUrl");
    }

    /// <summary>A non-loopback https configured value is still accepted: the loopback rule covers only the query override.</summary>
    [Fact]
    public void NonLoopbackHttpsConfigurationIsAccepted()
    {
        ApiBaseUrlResolver.Resolve("https://api.example.com/v1", "https://web.ompos.local/")
            .ShouldBe("https://api.example.com/v1");
    }

    /// <summary>An http URL to 127.0.0.1 or localhost is accepted.</summary>
    [Fact]
    public void LoopbackHttpVariantsAreAccepted()
    {
        ApiBaseUrlResolver.Validate("http://127.0.0.1:5187", false).ShouldBe("http://127.0.0.1:5187");
        ApiBaseUrlResolver.Validate("http://localhost:5001", false).ShouldBe("http://localhost:5001");
    }
}
