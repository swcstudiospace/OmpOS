using System.Net;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// Verifies the loopback check of the peer address. A real remote client cannot be produced on a machine whose listener binds
/// the loopback address only, so the decision is tested on the addresses a socket would report.
/// </summary>
public sealed class HookBridgePeerTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]
    [InlineData("127.255.255.254")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:127.1.2.3")]
    public void LoopbackAddressesAreAccepted(string address)
    {
        HookBridgeServer.IsLoopback(IPAddress.Parse(address)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("192.168.1.5")]
    [InlineData("10.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("128.0.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("2001:db8::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:192.168.1.5")]
    [InlineData("::ffff:10.0.0.1")]
    public void EveryOtherAddressIsRefused(string address)
    {
        HookBridgeServer.IsLoopback(IPAddress.Parse(address)).ShouldBeFalse();
    }

    [Fact]
    public void AnUnknownPeerIsRefused()
    {
        HookBridgeServer.IsLoopback(null).ShouldBeFalse();
    }
}
