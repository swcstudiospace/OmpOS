using System.Net;
using System.Text.Json;
using Autonogrammer.Harness.AgentLoop.Substrate;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// A live Substrate check. It runs only with <c>AUTONOGRAMMER_LIVE_SUBSTRATE=1</c> and a scratch URL, and it refuses port 7410 before any connection.
/// </summary>
public sealed class SubstrateLiveRefusalTests
{
    /// <summary>Verifies a scratch ledger accepts one note and reports its anchor status as devnet.</summary>
    [Fact]
    [Trait("Category", "Live")]
    public async Task ScratchLedger_WritesOneNote_AndReadsDevnetAnchorStatus()
    {
        Assert.SkipUnless(
            string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_SUBSTRATE"), "1", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_SUBSTRATE_URL")),
            "Set AUTONOGRAMMER_LIVE_SUBSTRATE=1 and AUTONOGRAMMER_LIVE_SUBSTRATE_URL to a scratch instance.");

        var url = Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_SUBSTRATE_URL");
        url.ShouldNotBeNullOrWhiteSpace();
        var endpoint = new Uri(url);
        endpoint.Port.ShouldNotBe(7410);
        var addresses = await Dns.GetHostAddressesAsync(endpoint.DnsSafeHost, TestContext.Current.CancellationToken);
        addresses.ShouldNotContain(
            address => address.Equals(IPAddress.Loopback) && endpoint.Port == 7410,
            "a URL that resolves to 127.0.0.1:7410 is refused before connecting");

        using var client = SubstrateGateway.CreateHttpClient();
        var gateway = new SubstrateGateway(
            client,
            new SubstrateEndpoint(endpoint),
            static () => Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_SUBSTRATE_TOKEN"),
            TimeProvider.System);
        var emit = await gateway.EmitEventAsync(
            new SubstrateEventEmit
            {
                Kind = "note",
                Summary = "audit",
                Payload = new SubstrateEventPayload { Action = "autonogrammer.audit.v1", Item = "01ARZ3NDEKTSV4RRFFQ69G5FAV", Outcome = "accepted" },
            },
            TestContext.Current.CancellationToken);
        emit.Stored.ShouldBeTrue();
        using var arguments = JsonDocument.Parse($$"""{"id":"{{emit.Id}}"}""");
        var anchor = await gateway.CallToolAsync("checkpoint_verify", arguments.RootElement, TestContext.Current.CancellationToken);
        anchor.IsError.ShouldBeFalse();
        anchor.Text.ShouldContain("devnet");
    }
}
