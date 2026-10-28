using OmpOS.Api.Services.Providers;
using OmpOS.Contracts.Providers;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Providers;

/// <summary>The live-test probe refuses an address on the service's own machine or network before any connection is made.</summary>
public sealed class HttpProviderProbeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("http://127.0.0.1:9/")]
    [InlineData("https://10.0.0.5/v1")]
    [InlineData("https://192.168.1.1/")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://100.100.100.200/")]
    [InlineData("http://[::1]:9/")]
    [InlineData("https://[fd00:ec2::254]/")]
    [InlineData("https://[::ffff:10.0.0.1]/")]
    public async Task AnAddressThatIsNotPublicIsBlockedWithoutAConnection(string address)
    {
        using var probe = new HttpProviderProbe(Microsoft.Extensions.Options.Options.Create(new ProvidersOptions()));

        var result = await probe.ProbeAsync(new Uri(address), Token).ConfigureAwait(true);

        result.Status.ShouldBe(ProviderTestStatus.Blocked);
        result.Reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ANameThatResolvesToTheLoopbackIsBlockedAtTheConnection()
    {
        using var probe = new HttpProviderProbe(Microsoft.Extensions.Options.Options.Create(new ProvidersOptions()));

        var result = await probe.ProbeAsync(new Uri("http://localhost:9/"), Token).ConfigureAwait(true);

        result.Status.ShouldBe(ProviderTestStatus.Blocked);
    }
}
