using Autonogrammer.Api.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Hosting;

/// <summary>Pins the defaults and the validation of <see cref="HostingOptions"/>.</summary>
public sealed class HostingOptionsTests
{
    private static readonly HostingOptionsValidator Validator = new();

    /// <summary>The defaults are valid and safe: no proxy trust, HSTS for a year, 30 s timeouts, a 15 s heartbeat.</summary>
    [Fact]
    public void Defaults_AreValidAndSafe()
    {
        var options = new HostingOptions();

        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
        options.ForwardedHeaders.Mode.ShouldBe(ForwardedHeadersMode.None);
        options.Hsts.Enabled.ShouldBeTrue();
        options.Hsts.MaxAgeDays.ShouldBe(365);
        options.SecurityHeaders.Enabled.ShouldBeTrue();
        options.RequestTimeoutSeconds.ShouldBe(30);
        options.ShutdownTimeoutSeconds.ShouldBe(30);
        options.Sse.HeartbeatSeconds.ShouldBe(15);
        options.Kestrel.MaxConcurrentConnections.ShouldBeNull();
        options.JsonLogs.ShouldBeNull();
    }

    /// <summary>A nested value outside its range is reported with the key that names it.</summary>
    [Fact]
    public void NestedValueOutOfRange_IsRejected()
    {
        var options = new HostingOptions();
        options.Kestrel.RequestHeadersTimeoutSeconds = 0;
        options.Hsts.MaxAgeDays = 0;
        options.Sse.HeartbeatSeconds = -1;

        var result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(static failure => failure.Contains("Hosting:Kestrel:RequestHeadersTimeoutSeconds", StringComparison.Ordinal));
        result.Failures.ShouldContain(static failure => failure.Contains("Hosting:Hsts:MaxAgeDays", StringComparison.Ordinal));
        result.Failures.ShouldContain(static failure => failure.Contains("Hosting:Sse:HeartbeatSeconds", StringComparison.Ordinal));
    }

    /// <summary>A top-level value outside its range fails data-annotation validation.</summary>
    [Fact]
    public void TopLevelValueOutOfRange_FailsDataAnnotations()
    {
        var options = new HostingOptions { RequestTimeoutSeconds = 0 };

        var result = new DataAnnotationValidateOptions<HostingOptions>(null).Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    /// <summary>The known-networks mode needs at least one CIDR range.</summary>
    [Fact]
    public void KnownNetworksModeWithoutRanges_IsRejected()
    {
        var options = new HostingOptions();
        options.ForwardedHeaders.Mode = ForwardedHeadersMode.KnownNetworks;

        Validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    /// <summary>An entry that is not a CIDR range is rejected; a valid range passes.</summary>
    [Theory]
    [InlineData("10.0.0.0/8", true)]
    [InlineData("fd00::/8", true)]
    [InlineData("not-a-network", false)]
    [InlineData("10.0.0.1/40", false)]
    public void KnownNetworks_MustBeCidrRanges(string network, bool valid)
    {
        var options = new HostingOptions();
        options.ForwardedHeaders.Mode = ForwardedHeadersMode.KnownNetworks;
        options.ForwardedHeaders.KnownNetworks.Add(network);

        Validator.Validate(null, options).Succeeded.ShouldBe(valid);
    }
}
