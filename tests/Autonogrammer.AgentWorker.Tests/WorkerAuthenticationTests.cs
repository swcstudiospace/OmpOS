using System.Net;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using Shouldly;
using Xunit;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>
/// The bridge's authentication boundary, observed over plain HTTP. A request without WebSocket headers is answered after authentication:
/// 401 for a token that fails validation, 403 for a valid token whose subject this worker does not serve, 400 ("not a WebSocket request")
/// once the token is accepted. Tokens are minted relative to the worker's fixed clock.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class WorkerAuthenticationTests
{
    private const string Session = "session-1";

    /// <summary>A token that satisfies every rule gets past authentication.</summary>
    [Fact]
    public async Task ValidTokenIsAuthenticatedAndReachesTheUpgradeCheck()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        var status = await StatusAsync(host, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A request without a bearer token is challenged.</summary>
    [Fact]
    public async Task MissingTokenIsRefusedWith401()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var host = await WorkerHost.StartAsync(tokens, new FixedTimeProvider(), cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        var status = await StatusAsync(host, token: null, timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Every way a token can break a rule is refused with 401.</summary>
    /// <param name="scenario">The rule the token breaks.</param>
    [Theory]
    [InlineData("expired")]
    [InlineData("expired-just-beyond-skew")]
    [InlineData("wrong-audience")]
    [InlineData("missing-audience")]
    [InlineData("wrong-issuer")]
    [InlineData("missing-issuer")]
    [InlineData("missing-expiry")]
    [InlineData("missing-issued-at")]
    [InlineData("lifetime-over-15-minutes")]
    [InlineData("issued-in-the-future")]
    [InlineData("foreign-signature")]
    [InlineData("hmac-signed-with-public-key")]
    [InlineData("not-a-token")]
    public async Task BrokenTokenIsRefusedWith401(string scenario)
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        using var foreign = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var now = clock.GetUtcNow();
        var token = scenario switch
        {
            "expired" => tokens.Mint(Session, now, lifetime: TimeSpan.FromMinutes(5), issuedAgo: TimeSpan.FromMinutes(10)),
            "expired-just-beyond-skew" => tokens.Mint(Session, now, lifetime: TimeSpan.FromMinutes(5), issuedAgo: TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(31)),
            "wrong-audience" => tokens.Mint(Session, now, audience: "autonogrammer-api"),
            "missing-audience" => tokens.Mint(Session, now, audience: null),
            "wrong-issuer" => tokens.Mint(Session, now, issuer: "someone-else"),
            "missing-issuer" => tokens.Mint(Session, now, issuer: null),
            "missing-expiry" => tokens.Mint(Session, now, includeExpiry: false),
            "missing-issued-at" => tokens.Mint(Session, now, includeIssuedAt: false),
            "lifetime-over-15-minutes" => tokens.Mint(Session, now, lifetime: TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1)),
            "issued-in-the-future" => tokens.Mint(Session, now, issuedAgo: TimeSpan.FromMinutes(-5)),
            "foreign-signature" => foreign.Mint(Session, now),
            "hmac-signed-with-public-key" => tokens.MintHmacSignedWithPublicKey(Session, now),
            _ => "not.a.token",
        };

        var status = await StatusAsync(host, token, timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>The lifetime limit is inclusive: a token of exactly 15 minutes is accepted, and one still inside the skew after expiry is accepted.</summary>
    /// <param name="scenario">The boundary being checked.</param>
    [Theory]
    [InlineData("exactly-15-minutes")]
    [InlineData("expired-within-skew")]
    [InlineData("issued-within-skew-in-the-future")]
    public async Task TokenOnTheBoundaryIsAccepted(string scenario)
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var now = clock.GetUtcNow();
        var token = scenario switch
        {
            "exactly-15-minutes" => tokens.Mint(Session, now, lifetime: TimeSpan.FromMinutes(15)),
            "expired-within-skew" => tokens.Mint(Session, now, lifetime: TimeSpan.FromMinutes(5), issuedAgo: TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(20)),
            _ => tokens.Mint(Session, now, issuedAgo: TimeSpan.FromSeconds(-20)),
        };

        var status = await StatusAsync(host, token, timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A token stops working when the worker's clock passes its expiry, and works again only with a fresh token.</summary>
    [Fact]
    public async Task TokenExpiresAsTheWorkerClockAdvances()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var token = tokens.Mint(Session, clock.GetUtcNow(), lifetime: TimeSpan.FromMinutes(5));

        (await StatusAsync(host, token, timeout.Token).ConfigureAwait(true)).ShouldBe(HttpStatusCode.BadRequest);
        clock.Advance(TimeSpan.FromMinutes(6));
        (await StatusAsync(host, token, timeout.Token).ConfigureAwait(true)).ShouldBe(HttpStatusCode.Unauthorized);
        var fresh = tokens.Mint(Session, clock.GetUtcNow());
        (await StatusAsync(host, fresh, timeout.Token).ConfigureAwait(true)).ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A subject that is not a safe path segment is refused with 403 even though the signature is valid.</summary>
    /// <param name="subject">The unsafe subject.</param>
    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData(".hidden")]
    [InlineData("has space")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public async Task UnsafeSubjectIsRefusedWith403(string subject)
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        var status = await StatusAsync(host, tokens.Mint(subject, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>A worker pinned to one session accepts that subject and refuses every other with 403.</summary>
    [Fact]
    public async Task PinnedWorkerServesOnlyItsSession()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Worker:SessionId"] = Session };
        var host = await WorkerHost.StartAsync(tokens, clock, settings: settings, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        (await StatusAsync(host, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true)).ShouldBe(HttpStatusCode.BadRequest);
        (await StatusAsync(host, tokens.Mint("session-2", clock.GetUtcNow()), timeout.Token).ConfigureAwait(true)).ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>A token carrying no subject is refused with 403.</summary>
    [Fact]
    public async Task TokenWithoutSubjectIsRefusedWith403()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        var status = await StatusAsync(host, tokens.Mint(string.Empty, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpStatusCode> StatusAsync(WorkerHost host, string? token, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = host.Address };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/acp");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode;
    }
}
