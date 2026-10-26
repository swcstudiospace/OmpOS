using System.Net;
using System.Net.Sockets;
using System.Text;
using OmpOS.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Verifies that the agentOS bearer token reaches only the configured origin: a cleartext origin is accepted only on
/// loopback, and a redirect or a response served from another host is refused (finding M-13).
/// </summary>
public sealed class AgentOsOriginTests
{
    private const string SecretToken = "sk_test_secret";
    private static readonly string[] ConfiguredHostOnly = ["api.rivet.dev"];
    private static readonly string?[] TokenOnly = [SecretToken];

    [Theory]
    [InlineData("http://api.rivet.dev")]
    [InlineData("http://203.0.113.9:6420")]
    [InlineData("ftp://localhost")]
    public void NonLoopbackCleartextOriginIsRefusedAtConstruction(string endpoint)
    {
        using var handler = new FixedHandler(HttpStatusCode.OK);
        using var http = new HttpClient(handler);

        var error = Should.Throw<ArgumentException>(
            () => new AgentOsProvider(http, new Uri(endpoint), "ns-test", SecretToken));

        error.ParamName.ShouldBe("endpoint");
        error.Message.ShouldNotContain(SecretToken, Case.Sensitive);
    }

    [Theory]
    [InlineData("https://api.rivet.dev")]
    [InlineData("http://localhost:6420")]
    [InlineData("http://127.0.0.1:6420")]
    [InlineData("http://[::1]:6420")]
    public void HttpsAndLoopbackOriginsAreAccepted(string endpoint)
    {
        using var handler = new FixedHandler(HttpStatusCode.OK);
        using var http = new HttpClient(handler);

        Should.NotThrow(() => new AgentOsProvider(http, new Uri(endpoint), "ns-test", SecretToken));
    }

    [Fact]
    public async Task RedirectResponseIsRefusedAndNoSecondRequestCarriesTheToken()
    {
        using var handler = new FixedHandler(HttpStatusCode.TemporaryRedirect) { Location = new Uri("https://attacker.example/steal") };
        using var http = new HttpClient(handler);
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), "ns-test", SecretToken);

        var error = await Should.ThrowAsync<NotAvailableException>(
            () => provider.TeardownAsync("vm-1", TestContext.Current.CancellationToken));

        error.Message.ShouldNotContain(SecretToken, Case.Sensitive);
        handler.RequestHosts.ShouldBe(ConfiguredHostOnly);
    }

    [Fact]
    public async Task ResponseServedFromAnotherOriginIsRefused()
    {
        using var handler = new FixedHandler(HttpStatusCode.OK) { ServedFrom = new Uri("https://attacker.example/actors/vm-1") };
        using var http = new HttpClient(handler);
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), "ns-test", SecretToken);

        await Should.ThrowAsync<NotAvailableException>(
            () => provider.AttachAsync("vm-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TokenIsSentOnlyToTheConfiguredHost()
    {
        using var handler = new FixedHandler(HttpStatusCode.NotFound);
        using var http = new HttpClient(handler);
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), "ns-test", SecretToken);

        await provider.TeardownAsync("vm-1", TestContext.Current.CancellationToken);

        handler.RequestHosts.ShouldBe(ConfiguredHostOnly);
        handler.BearerTokens.ShouldBe(TokenOnly);
    }

    [Fact]
    public async Task FactoryClientNeverContactsTheRedirectTarget()
    {
        using var target = new TcpListener(IPAddress.Loopback, 0);
        target.Start();
        var targetPort = ((IPEndPoint)target.LocalEndpoint).Port;
        using var origin = new TcpListener(IPAddress.Loopback, 0);
        origin.Start();
        var originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
        using var http = AgentOsProvider.CreateHttpClient();
        var provider = new AgentOsProvider(http, new Uri($"http://127.0.0.1:{originPort}"), "ns-test", SecretToken);

        var served = ServeAsync();
        try
        {
            await Should.ThrowAsync<NotAvailableException>(
                () => provider.TeardownAsync("vm-1", TestContext.Current.CancellationToken)).ConfigureAwait(true);
        }
        finally
        {
            await served.ConfigureAwait(true);
        }

        target.Pending().ShouldBeFalse();

        async Task ServeAsync()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var client = await origin.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            var stream = client.GetStream();
            var buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            var response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{targetPort}/stolen\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class FixedHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public Uri? Location { get; init; }

        public Uri? ServedFrom { get; init; }

        public List<string> RequestHosts { get; } = [];

        public List<string?> BearerTokens { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestHosts.Add(request.RequestUri!.Host);
            BearerTokens.Add(request.Headers.Authorization?.Parameter);
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                RequestMessage = ServedFrom is null ? request : new HttpRequestMessage(request.Method, ServedFrom),
            };
            response.Headers.Location = Location;
            return Task.FromResult(response);
        }
    }
}
