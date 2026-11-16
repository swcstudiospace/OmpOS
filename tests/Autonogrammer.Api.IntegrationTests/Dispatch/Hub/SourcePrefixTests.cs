using System.Net;
using System.Security.Cryptography;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>The recorded source prefix: an IPv4 address is kept as its /24 and an IPv6 address as its /48, on the device row only (D-12, D-44).</summary>
public sealed class SourcePrefixTests
{
    /// <summary>An IPv4 address is recorded as its /24 network.</summary>
    [Fact]
    public void An_ipv4_address_is_recorded_as_its_slash_24_prefix()
    {
        DispatchSourcePrefix.Of(IPAddress.Parse("203.0.113.77")).ShouldBe("203.0.113.0/24");
    }

    /// <summary>An IPv6 address is recorded as its /48 network.</summary>
    [Fact]
    public void An_ipv6_address_is_recorded_as_its_slash_48_prefix()
    {
        DispatchSourcePrefix.Of(IPAddress.Parse("2001:db8:abcd:1234::1")).ShouldBe("2001:db8:abcd::/48");
    }

    /// <summary>An IPv4-mapped IPv6 address is recorded as the IPv4 /24 it maps to.</summary>
    [Fact]
    public void An_ipv4_mapped_ipv6_address_is_recorded_as_its_ipv4_slash_24_prefix()
    {
        DispatchSourcePrefix.Of(IPAddress.Parse("::ffff:198.51.100.9")).ShouldBe("198.51.100.0/24");
    }

    /// <summary>A request with no remote address records no prefix.</summary>
    [Fact]
    public void A_missing_remote_address_records_no_prefix()
    {
        DispatchSourcePrefix.Of(null).ShouldBeNull();
    }

    /// <summary>A verified connection writes the prefix of its remote address to the device row.</summary>
    [Fact]
    public async Task A_verified_connection_writes_the_source_prefix_to_the_device_row()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);

        var client = host.CreateWebSocketClient();
        client.ConfigureRequest = request =>
        {
            request.Headers["Authorization"] = "Bearer " + device.Credential;
            request.Headers["X-Test-Remote-Address"] = "203.0.113.77";
        };
        using var socket = await client.ConnectAsync(new Uri("ws://localhost/dispatch/devices/" + device.DeviceId + "/connect"), ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        var store = host.Services.GetRequiredService<IDispatchStore>();
        var row = (await store.GetDeviceAsync(DispatchTestClient.Owner, device.DeviceId, ct).ConfigureAwait(true)).ShouldNotBeNull();
        row.LastSourcePrefix.ShouldBe("203.0.113.0/24");
    }
}
