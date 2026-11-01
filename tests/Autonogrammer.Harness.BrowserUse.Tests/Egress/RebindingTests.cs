using System.Net;
using Autonogrammer.Harness.BrowserUse.Egress;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using Autonogrammer.Harness.BrowserUse.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Egress;

/// <summary>A flipped second answer is never used. The dial pins to the validated address.</summary>
public sealed class RebindingTests
{
    [Fact]
    public async Task FlippedSecondAnswer_IsNotConsulted_AndTheDialPinsTheFirstAddress()
    {
        var first = IPAddress.Parse("1.1.1.1");
        var second = IPAddress.Parse("10.1.2.3");
        var resolver = new FakeHostResolver();
        resolver.Enqueue(first);
        resolver.Enqueue(second);
        var connector = new RecordingConnector();
        var stream = await PinnedDial.ConnectAsync(
            "cdn.example",
            443,
            resolver,
            new OriginGrants(),
            new BrowserUseOptions(),
            connector,
            dispatchTurn: false,
            CancellationToken.None).ConfigureAwait(true);

        stream.ShouldNotBeNull();
        await stream.DisposeAsync().ConfigureAwait(true);
        resolver.Calls.ShouldBe(1);
        connector.Endpoints.Count.ShouldBe(1);
        connector.Endpoints[0].Address.ShouldBe(first);
        connector.Endpoints[0].Port.ShouldBe(443);
        connector.Endpoints[0].Address.ShouldNotBe(second);
    }

    private sealed class RecordingConnector : ITcpConnector
    {
        public List<IPEndPoint> Endpoints { get; } = [];

        public ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
        {
            Endpoints.Add(endpoint);
            return ValueTask.FromResult<Stream>(new MemoryStream());
        }
    }
}
