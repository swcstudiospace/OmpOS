using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using OmpOS.Harness.Mcp;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.McpGateway.Tests;

/// <summary>Only a local client that holds the gateway secret can reach it, and only through the loopback interface.</summary>
public sealed class McpGatewayAccessTests
{
    private const string SessionA = "session-a";
    private const string AddCall = """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"fixture__add","arguments":{"a":1,"b":2}}}""";

    [Fact]
    public async Task GatewayBindsLoopbackOnly()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var local = harness.Gateway.LocalEndPoint;
            local.Address.ShouldBe(IPAddress.Loopback);
            local.Port.ShouldBeGreaterThan(0);
            harness.Gateway.OpenSession(SessionA).Host.ShouldBe("127.0.0.1");

            var listening = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Where(endpoint => endpoint.Port == local.Port).ToList();
            listening.ShouldNotBeEmpty();
            listening.ShouldAllBe(endpoint => IPAddress.IsLoopback(endpoint.Address));

            var external = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(unicast => unicast.Address)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                .ToList();
            foreach (var address in external)
            {
                using var probe = new TcpClient();
                await Should.ThrowAsync<SocketException>(() => probe.ConnectAsync(address, local.Port, TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);
            }
        }
    }

    [Fact]
    public async Task EveryGatewayListensOnItsOwnPort()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var other = McpGatewayServer.Start(harness.Registry, new McpToolInvoker(harness.Registry, harness.Approval.Approve));
            await using (other.ConfigureAwait(true))
            {
                other.LocalEndPoint.Port.ShouldNotBe(harness.Gateway.LocalEndPoint.Port);
                string.Equals(other.BearerToken, harness.Gateway.BearerToken, StringComparison.Ordinal).ShouldBeFalse("each gateway mints its own bearer token");
            }
        }
    }

    [Fact]
    public async Task RequestWithoutTheTokenIsRefused()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var reply = await harness.SendAsync(HttpMethod.Post, endpoint, AddCall, transport, null, TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(401);
            reply.Body.ShouldBeEmpty();
            reply.Headers.WwwAuthenticate.ToString().ShouldBe("Bearer");
            harness.Approval.Calls.ShouldBe(0);
            harness.Log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task InitializeWithoutTheTokenIssuesNoSession()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);

            var reply = await harness.SendAsync(
                HttpMethod.Post,
                endpoint,
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25"}}""",
                null,
                null,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(401);
            reply.SessionId.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData("Bearer wrong-value")]
    [InlineData("Bearer ")]
    [InlineData("Bearer")]
    [InlineData("bearer-without-space")]
    public async Task RequestWithAWrongTokenIsRefused(string authorization)
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var reply = await harness.SendAsync(HttpMethod.Post, endpoint, AddCall, transport, authorization, TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(401);
            harness.Approval.Calls.ShouldBe(0);
            harness.Log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task RequestWithTheRightSecretUnderAnotherSchemeIsRefused()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var reply = await harness.SendAsync(HttpMethod.Post, endpoint, AddCall, transport, "Basic " + harness.Gateway.BearerToken, TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(401);
            harness.Log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task OtherMethodsAndDeleteAreAlsoBehindTheToken()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var get = await harness.SendAsync(HttpMethod.Get, endpoint, null, null, null, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var delete = await harness.SendAsync(HttpMethod.Delete, endpoint, null, transport, null, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var stillLive = await harness.ListToolsAsync(endpoint, transport, TestContext.Current.CancellationToken).ConfigureAwait(true);

            get.Status.ShouldBe(401);
            delete.Status.ShouldBe(401);
            stillLive.Status.ShouldBe(200);
        }
    }

    [Fact]
    public async Task ForeignOriginIsForbiddenAndLoopbackOriginIsAllowed()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var foreign = await harness.SendAsync(HttpMethod.Post, endpoint, AddCall, transport, harness.Bearer, TestContext.Current.CancellationToken, request => request.Headers.TryAddWithoutValidation("Origin", "https://example.test")).ConfigureAwait(true);
            var opaque = await harness.SendAsync(HttpMethod.Post, endpoint, AddCall, transport, harness.Bearer, TestContext.Current.CancellationToken, request => request.Headers.TryAddWithoutValidation("Origin", "null")).ConfigureAwait(true);
            var local = await harness.SendAsync(HttpMethod.Post, endpoint, """{"jsonrpc":"2.0","id":1,"method":"ping"}""", transport, harness.Bearer, TestContext.Current.CancellationToken, request => request.Headers.TryAddWithoutValidation("Origin", "http://localhost:5173")).ConfigureAwait(true);

            foreign.Status.ShouldBe(403);
            opaque.Status.ShouldBe(403);
            local.Status.ShouldBe(200);
            harness.Log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ForeignHostHeaderIsRefused()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var reply = await harness.SendAsync(HttpMethod.Post, endpoint, AddCall, transport, harness.Bearer, TestContext.Current.CancellationToken, request => request.Headers.Host = "rebind.example.test").ConfigureAwait(true);

            // The managed listener answers a Host that matches none of its prefixes with 404 before the gateway's own 403 check runs; all three refuse the request.
            reply.Status.ShouldBeOneOf(400, 403, 404);
            harness.Log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task TheSecretIsNeverPartOfToString()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var text = harness.Gateway.ToString();

            text.ShouldNotContain(harness.Gateway.BearerToken, Case.Sensitive);
            text.ShouldContain("127.0.0.1:" + harness.Gateway.LocalEndPoint.Port, Case.Sensitive);
            harness.Gateway.BearerToken.Length.ShouldBeGreaterThanOrEqualTo(43);
        }
    }

    [Fact]
    public async Task DisposedGatewayStopsListening()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var port = harness.Gateway.LocalEndPoint.Port;
        var endpoint = harness.Gateway.OpenSession(SessionA);
        await harness.Gateway.DisposeAsync().ConfigureAwait(true);
        try
        {
            using var probe = new TcpClient();
            await Should.ThrowAsync<SocketException>(() => probe.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);
            Should.Throw<ObjectDisposedException>(() => harness.Gateway.OpenSession("another-session"));
            endpoint.Port.ShouldBe(port);
        }
        finally
        {
            await harness.DisposeAsync().ConfigureAwait(true);
        }
    }
}
