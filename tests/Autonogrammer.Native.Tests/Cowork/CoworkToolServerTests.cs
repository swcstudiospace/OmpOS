using Autonogrammer.Native.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>The gateway starts once per head. A health-check failure degrades; the bearer is not logged.</summary>
public sealed class CoworkToolServerTests
{
    [Fact]
    public async Task AHealthySessionCarriesTheBearerOnlyInTheEntry()
    {
        await using var server = new CoworkToolServer();
        var (entry, notice) = await server.OpenAsync("session-1", TestContext.Current.CancellationToken);
        notice.ShouldBeNull();
        entry.ShouldNotBeNull();
        entry.Url.Host.ShouldBe("127.0.0.1");
        entry.Bearer.ShouldNotBeNullOrWhiteSpace();
        server.ToString().ShouldNotContain(entry.Bearer);
        server.Gateway!.ToString().ShouldNotContain(entry.Bearer);
    }

    [Fact]
    public async Task AClosedGatewayDegradesInsteadOfFailingTheCaller()
    {
        await using var server = new CoworkToolServer();
        await server.DisposeAsync();
        var (entry, notice) = await server.OpenAsync("session-1", TestContext.Current.CancellationToken);
        entry.ShouldBeNull();
        notice.ShouldNotBeNull();
        notice.ShouldStartWith(CoworkToolServer.UnavailablePrefix);
    }
}
