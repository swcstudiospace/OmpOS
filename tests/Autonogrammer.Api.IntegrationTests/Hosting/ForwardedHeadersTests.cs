using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Hosting;

/// <summary>Pins forwarded-header handling: ignored by default, one hop honoured when trusted.</summary>
public sealed class ForwardedHeadersTests
{
    private static void MapEcho(WebApplication app) =>
        app.MapGet("/who", static (HttpContext context) =>
            $"{context.Connection.RemoteIpAddress}|{context.Request.Scheme}|{context.Request.Host}");

    private static async Task<string> GetWhoAsync(WebApplication app, CancellationToken testToken)
    {
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/who", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.7, 198.51.100.9");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "api.example.test");
        using var response = await client.SendAsync(request, testToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(false);
    }

    /// <summary>With the default mode the headers are ignored: the address is the peer and the scheme stays http.</summary>
    [Fact]
    public async Task ModeNone_IgnoresForwardedHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var app = await HostingTestHost.StartAsync(ct, mapEndpoints: MapEcho).ConfigureAwait(true);
        await using var appScope = app.ConfigureAwait(true);

        var who = await GetWhoAsync(app, ct).ConfigureAwait(true);

        who.ShouldNotContain("203.0.113.7");
        who.ShouldContain("|http|");
    }

    /// <summary>With TrustAllSingleHop exactly one hop is honoured: the last X-Forwarded-For entry, the scheme and the host.</summary>
    [Fact]
    public async Task TrustAllSingleHop_HonoursExactlyOneHop()
    {
        var ct = TestContext.Current.CancellationToken;
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Hosting:ForwardedHeaders:Mode"] = "TrustAllSingleHop" };
        var app = await HostingTestHost.StartAsync(ct, config: config, mapEndpoints: MapEcho).ConfigureAwait(true);
        await using var appScope = app.ConfigureAwait(true);

        var who = await GetWhoAsync(app, ct).ConfigureAwait(true);

        who.ShouldBe("198.51.100.9|https|api.example.test");
    }

    /// <summary>With KnownNetworks a peer outside the listed ranges is not trusted: its headers are ignored.</summary>
    [Fact]
    public async Task KnownNetworks_IgnoresHeadersFromAnUntrustedPeer()
    {
        var ct = TestContext.Current.CancellationToken;
        var app = await HostingTestHost.StartAsync(ct, config: KnownNetworksConfig(), configureServices: PeerAt("203.0.113.50"), mapEndpoints: MapEcho).ConfigureAwait(true);
        await using var appScope = app.ConfigureAwait(true);

        var who = await GetWhoAsync(app, ct).ConfigureAwait(true);

        who.ShouldNotContain("198.51.100.9");
        who.ShouldStartWith("203.0.113.50|http|");
    }

    /// <summary>With KnownNetworks a peer inside a listed range is trusted for one hop.</summary>
    [Fact]
    public async Task KnownNetworks_HonoursHeadersFromATrustedPeer()
    {
        var ct = TestContext.Current.CancellationToken;
        var app = await HostingTestHost.StartAsync(ct, config: KnownNetworksConfig(), configureServices: PeerAt("192.0.2.10"), mapEndpoints: MapEcho).ConfigureAwait(true);
        await using var appScope = app.ConfigureAwait(true);

        var who = await GetWhoAsync(app, ct).ConfigureAwait(true);

        who.ShouldBe("198.51.100.9|https|api.example.test");
    }

    private static Dictionary<string, string?> KnownNetworksConfig() => new(StringComparer.Ordinal)
    {
        ["Hosting:ForwardedHeaders:Mode"] = "KnownNetworks",
        ["Hosting:ForwardedHeaders:KnownNetworks:0"] = "192.0.2.0/24",
    };

    // The test server has no remote address of its own; this gives the connection one before the pipeline starts, as the socket would.
    private static Action<IServiceCollection> PeerAt(string address) =>
        services => services.AddTransient<IStartupFilter>(_ => new PeerAddressFilter(IPAddress.Parse(address)));

    private sealed class PeerAddressFilter : IStartupFilter
    {
        private readonly IPAddress _address;

        public PeerAddressFilter(IPAddress address)
        {
            _address = address;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            ArgumentNullException.ThrowIfNull(next);
            return app =>
            {
                app.Use((context, nextDelegate) =>
                {
                    context.Connection.RemoteIpAddress = _address;
                    return nextDelegate(context);
                });
                next(app);
            };
        }
    }
}
