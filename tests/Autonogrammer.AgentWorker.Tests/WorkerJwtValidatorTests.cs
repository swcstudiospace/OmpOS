using System.Security.Claims;
using System.Security.Cryptography;
using Autonogrammer.AgentWorker.Configuration;
using Autonogrammer.AgentWorker.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>The token rules at the validator itself, below the HTTP pipeline: algorithms, the clock and the subject.</summary>
public sealed class WorkerJwtValidatorTests
{
    /// <summary>A token signed by the configured key for the audience and issuer validates.</summary>
    [Fact]
    public async Task ValidTokenValidates()
    {
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        using var validator = CreateValidator(tokens, clock);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(tokens.Mint("session-1", clock.GetUtcNow()), validator.Parameters).ConfigureAwait(true);

        result.IsValid.ShouldBeTrue();
    }

    /// <summary>An unsigned token (<c>alg: none</c>) is refused.</summary>
    [Fact]
    public async Task UnsignedTokenIsRefused()
    {
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        using var validator = CreateValidator(tokens, clock);
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode(
            FormattableString.Invariant($"{{\"sub\":\"session-1\",\"iss\":\"autonogrammer-api\",\"aud\":\"autonogrammer-worker\",\"iat\":{now},\"exp\":{now + 300}}}"));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(header + "." + payload + ".", validator.Parameters).ConfigureAwait(true);

        result.IsValid.ShouldBeFalse();
    }

    /// <summary>An HS256 token keyed with the public key text is refused: the algorithm allow-list defeats algorithm confusion.</summary>
    [Fact]
    public async Task HmacTokenKeyedWithThePublicKeyIsRefused()
    {
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        using var validator = CreateValidator(tokens, clock);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(tokens.MintHmacSignedWithPublicKey("session-1", clock.GetUtcNow()), validator.Parameters).ConfigureAwait(true);

        result.IsValid.ShouldBeFalse();
    }

    /// <summary>The validator reads time only from the injected provider: the same token is valid, then expired, as that clock moves.</summary>
    [Fact]
    public async Task LifetimeFollowsTheInjectedClock()
    {
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        using var validator = CreateValidator(tokens, clock);
        var token = tokens.Mint("session-1", clock.GetUtcNow(), lifetime: TimeSpan.FromMinutes(10));
        var handler = new JsonWebTokenHandler();

        (await handler.ValidateTokenAsync(token, validator.Parameters).ConfigureAwait(true)).IsValid.ShouldBeTrue();
        clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(30));
        (await handler.ValidateTokenAsync(token, validator.Parameters).ConfigureAwait(true)).IsValid.ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(1));
        (await handler.ValidateTokenAsync(token, validator.Parameters).ConfigureAwait(true)).IsValid.ShouldBeFalse();
    }

    /// <summary>The subject of an unpinned worker is accepted when safe; a pinned worker accepts only its session.</summary>
    [Fact]
    public void SubjectIsCheckedAgainstTheSafeShapeAndThePin()
    {
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        using var pooled = CreateValidator(tokens, clock);
        using var pinned = CreateValidator(tokens, clock, pinnedSession: "session-1");

        pooled.TryGetSessionId(Principal("session-1"), out var pooledSession).ShouldBeTrue();
        pooledSession.ShouldBe("session-1");
        pooled.TryGetSessionId(Principal("../x"), out _).ShouldBeFalse();
        pooled.TryGetSessionId(new ClaimsPrincipal(new ClaimsIdentity()), out _).ShouldBeFalse();
        pinned.TryGetSessionId(Principal("session-1"), out _).ShouldBeTrue();
        pinned.TryGetSessionId(Principal("session-2"), out _).ShouldBeFalse();
    }

    /// <summary>ECDSA and RSA (2048 bits or more) public keys parse, in any order and count, and an empty list is refused.</summary>
    [Fact]
    public void KeySetParsesEcdsaAndRsaPublicKeys()
    {
        using var tokens = new WorkerTokenFactory();
        using var rsa = RSA.Create(2048);

        using var keys = WorkerVerificationKeySet.Parse([rsa.ExportSubjectPublicKeyInfoPem(), tokens.PublicKeyPem]);

        keys.Keys.Count.ShouldBe(2);
        keys.Keys[0].ShouldBeOfType<RsaSecurityKey>();
        keys.Keys[1].ShouldBeOfType<ECDsaSecurityKey>();
        Should.Throw<FormatException>(() => WorkerVerificationKeySet.Parse([]));
    }

    private static WorkerJwtValidator CreateValidator(WorkerTokenFactory tokens, FixedTimeProvider clock, string? pinnedSession = null)
    {
        var auth = new WorkerAuthOptions { PublicKeysPem = { tokens.PublicKeyPem } };
        var worker = new WorkerOptions { SessionId = pinnedSession };
        return new WorkerJwtValidator(Options.Create(auth), Options.Create(worker), clock);
    }

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "test"));
}
