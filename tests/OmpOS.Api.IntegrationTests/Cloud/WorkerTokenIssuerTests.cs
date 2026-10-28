using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OmpOS.Api.Cloud;
using OmpOS.Api.Cloud.Options;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Cloud;

/// <summary>The worker token: ES256, the audience, issuer and subject a worker requires, a lifetime of at most fifteen minutes measured on the injected clock, and a key that is checked when the issuer is built.</summary>
public sealed class WorkerTokenIssuerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A token carries exactly the claims the worker validates and verifies with the public half of the key.</summary>
    [Fact]
    public async Task Issue_ProducesAnEs256TokenWithTheWorkerContract()
    {
        using var keys = new CloudTestKeys();
        var clock = new ManualClock(Start);
        using var issuer = Create(keys.SigningKeyPem, TimeSpan.FromMinutes(5), clock);

        var token = issuer.Issue("cloud-session-1");

        var parsed = new JsonWebTokenHandler().ReadJsonWebToken(token);
        parsed.Alg.ShouldBe("ES256");
        parsed.Issuer.ShouldBe("ompos-api");
        parsed.Audiences.ShouldHaveSingleItem().ShouldBe("ompos-worker");
        parsed.Subject.ShouldBe("cloud-session-1");
        new DateTimeOffset(parsed.IssuedAt).ShouldBe(Start);
        new DateTimeOffset(parsed.ValidTo).ShouldBe(Start + TimeSpan.FromMinutes(5));
        var validated = await new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = "ompos-api",
                ValidAudience = "ompos-worker",
                IssuerSigningKey = keys.WorkerVerificationKey,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                LifetimeValidator = TestTokenLifetime.Of(clock),
            }).ConfigureAwait(true);
        validated.IsValid.ShouldBeTrue(validated.Exception?.Message);
    }

    /// <summary>A token issued after the clock moved expires later: time is the injected clock, and every token is unique.</summary>
    [Fact]
    public void Issue_FollowsTheInjectedClockAndIsUnique()
    {
        using var keys = new CloudTestKeys();
        var clock = new ManualClock(Start);
        using var issuer = Create(keys.SigningKeyPem, TimeSpan.FromMinutes(15), clock);
        var handler = new JsonWebTokenHandler();

        var first = issuer.Issue("s");
        var same = issuer.Issue("s");
        clock.Advance(TimeSpan.FromHours(1));
        var later = issuer.Issue("s");

        string.Equals(first, same, StringComparison.Ordinal).ShouldBeFalse("two tokens issued at one instant must still differ");
        new DateTimeOffset(handler.ReadJsonWebToken(first).ValidTo).ShouldBe(Start + TimeSpan.FromMinutes(15));
        new DateTimeOffset(handler.ReadJsonWebToken(later).ValidTo).ShouldBe(Start + TimeSpan.FromHours(1) + TimeSpan.FromMinutes(15));
        issuer.NextExpiry().ShouldBe(Start + TimeSpan.FromHours(1) + TimeSpan.FromMinutes(15));
        (new DateTimeOffset(handler.ReadJsonWebToken(later).ValidTo) - new DateTimeOffset(handler.ReadJsonWebToken(later).IssuedAt)).ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
    }

    /// <summary>A lifetime the worker would refuse (over fifteen minutes, or not positive) stops the issuer from being built.</summary>
    [Theory]
    [InlineData(16 * 60)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Lifetime_OutsideTheWorkerWindow_IsRefused(int seconds)
    {
        using var keys = new CloudTestKeys();
        var clock = new ManualClock(Start);

        Should.Throw<InvalidOperationException>(() =>
        {
            using var issuer = Create(keys.SigningKeyPem, TimeSpan.FromSeconds(seconds), clock);
        });
    }

    /// <summary>A key that is missing, is not a private key, or is not an EC P-256 key is refused when the issuer is built, with a message that names the key and not its content.</summary>
    [Fact]
    public void Key_ThatIsNotEcP256_IsRefused()
    {
        var clock = new ManualClock(Start);
        using var rsa = RSA.Create(2048);
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        using var keys = new CloudTestKeys();
        var bad = new[] { string.Empty, "not a pem", rsa.ExportPkcs8PrivateKeyPem(), p384.ExportPkcs8PrivateKeyPem(), keys.WorkerPublicKeyPem };

        foreach (var pem in bad)
        {
            var failure = Should.Throw<InvalidOperationException>(() =>
            {
                using var issuer = Create(pem, TimeSpan.FromMinutes(5), clock);
            });
            failure.Message.ShouldContain("Cloud:Worker:SigningKeyPem", Case.Sensitive);
            if (pem.Length > 0)
            {
                failure.Message.ShouldNotContain(pem, Case.Sensitive);
            }
        }
    }

    /// <summary>A PEM whose line breaks were written as the two characters backslash and n, as many environment files do, is accepted.</summary>
    [Fact]
    public void Key_WithEscapedLineBreaks_IsAccepted()
    {
        using var keys = new CloudTestKeys();
        var clock = new ManualClock(Start);
        using var issuer = Create(keys.SigningKeyPem.Replace("\n", "\\n", StringComparison.Ordinal), TimeSpan.FromMinutes(5), clock);

        new JsonWebTokenHandler().ReadJsonWebToken(issuer.Issue("s")).Alg.ShouldBe("ES256");
    }

    /// <summary>The issuer never prints its key.</summary>
    [Fact]
    public void ToString_DoesNotRevealTheKey()
    {
        using var keys = new CloudTestKeys();
        using var issuer = Create(keys.SigningKeyPem, TimeSpan.FromMinutes(5), new ManualClock(Start));

        issuer.ToString().ShouldNotContain("PRIVATE", Case.Sensitive);
    }

    private static WorkerTokenIssuer Create(string pem, TimeSpan lifetime, TimeProvider clock) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new CloudOptions { Worker = { SigningKeyPem = pem, TokenLifetime = lifetime } }),
            clock);
}
