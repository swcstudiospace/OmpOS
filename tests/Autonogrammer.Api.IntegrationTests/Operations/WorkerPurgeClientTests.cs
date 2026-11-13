using System.Net;
using Autonogrammer.Api.Cloud;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Api.IntegrationTests.Cloud;
using Autonogrammer.Api.Operations;
using Autonogrammer.Api.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Operations;

/// <summary>
/// The worker purge. The purge token carries the contract claims and verifies with the control plane's key, and it is sent as the bearer of
/// <c>DELETE /accounts/{ownerDir}</c>. Only 204 and 404 are outcomes; every other answer, including 409 when a session of the owner is attached, fails.
/// </summary>
public sealed class WorkerPurgeClientTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly Uri WorkerBase = new("http://worker.test:8080");
    private const string Owner = "alice";

    /// <summary>The purge token carries the purge audience, the purpose, the owner directory and a five-minute lifetime, and verifies with the issuer's public key.</summary>
    [Fact]
    public async Task IssuePurge_CarriesTheContractClaims_AndVerifiesWithTheIssuerKey()
    {
        using var keys = new CloudTestKeys();
        var clock = new ManualClock(Start);
        using var issuer = Create(keys, clock);
        var ownerDir = AccountWorkspace.DirectoryName(Owner);

        var token = issuer.IssuePurge(ownerDir);

        var parsed = new JsonWebTokenHandler().ReadJsonWebToken(token);
        parsed.Alg.ShouldBe("ES256");
        parsed.Issuer.ShouldBe(Autonogrammer.Api.LegacyBrandNames.WorkerIssuer);
        parsed.Audiences.ShouldHaveSingleItem().ShouldBe("autonogrammer-worker-purge");
        parsed.GetPayloadValue<string>("purpose").ShouldBe("purge");
        parsed.GetPayloadValue<string>("owner_dir").ShouldBe(ownerDir);
        new DateTimeOffset(parsed.IssuedAt).ShouldBe(Start);
        new DateTimeOffset(parsed.ValidTo).ShouldBe(Start + TimeSpan.FromMinutes(5));
        var validated = await new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = Autonogrammer.Api.LegacyBrandNames.WorkerIssuer,
                ValidAudience = "autonogrammer-worker-purge",
                IssuerSigningKey = keys.WorkerVerificationKey,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                LifetimeValidator = TestTokenLifetime.Of(clock),
            }).ConfigureAwait(true);
        validated.IsValid.ShouldBeTrue(validated.Exception?.Message);
    }

    /// <summary>The purge is a DELETE of the owner's folder on the worker, authorised by a purge token as the bearer, and a 204 is a purge.</summary>
    [Fact]
    public async Task Purge_SendsADeleteWithAPurgeTokenAsTheBearer()
    {
        var ct = TestContext.Current.CancellationToken;
        using var keys = new CloudTestKeys();
        using var issuer = Create(keys, new ManualClock(Start));
        var ownerDir = AccountWorkspace.DirectoryName(Owner);
        using var handler = new RecordingHandler(HttpStatusCode.NoContent);
        using var http = new HttpClient(handler);
        var client = new WorkerPurgeClient(http, issuer);

        var outcome = await client.PurgeAsync(WorkerBase, ownerDir, ct).ConfigureAwait(true);

        outcome.ShouldBe(WorkerPurgeOutcome.Purged);
        handler.Method.ShouldBe(HttpMethod.Delete);
        handler.Target.ShouldBe(new Uri($"http://worker.test:8080/accounts/{ownerDir}"));
        handler.Scheme.ShouldBe("Bearer");
        new JsonWebTokenHandler().ReadJsonWebToken(handler.Parameter!).GetPayloadValue<string>("purpose").ShouldBe("purge");
    }

    /// <summary>A 404 means the worker had no folder for the account, which is not a failure.</summary>
    [Fact]
    public async Task Purge_AnAbsentFolder_IsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var keys = new CloudTestKeys();
        using var issuer = Create(keys, new ManualClock(Start));
        using var handler = new RecordingHandler(HttpStatusCode.NotFound);
        using var http = new HttpClient(handler);
        var client = new WorkerPurgeClient(http, issuer);

        var outcome = await client.PurgeAsync(WorkerBase, AccountWorkspace.DirectoryName(Owner), ct).ConfigureAwait(true);

        outcome.ShouldBe(WorkerPurgeOutcome.NotFound);
    }

    /// <summary>Any answer other than 204 or 404 fails, including 409 when a session of the owner is attached, so erasure fails closed.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Purge_AnyOtherAnswer_IsAFailure(HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        using var keys = new CloudTestKeys();
        using var issuer = Create(keys, new ManualClock(Start));
        using var handler = new RecordingHandler(status);
        using var http = new HttpClient(handler);
        var client = new WorkerPurgeClient(http, issuer);

        var failure = await Should.ThrowAsync<WorkerPurgeException>(() => client.PurgeAsync(WorkerBase, AccountWorkspace.DirectoryName(Owner), ct)).ConfigureAwait(true);

        failure.StatusCode.ShouldBe(status);
    }

    /// <summary>An owner directory that is not 64 lowercase hexadecimal characters is refused before anything is sent.</summary>
    [Fact]
    public async Task Purge_MalformedOwnerDir_IsRefusedBeforeSending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var keys = new CloudTestKeys();
        using var issuer = Create(keys, new ManualClock(Start));
        using var handler = new RecordingHandler(HttpStatusCode.NoContent);
        using var http = new HttpClient(handler);
        var client = new WorkerPurgeClient(http, issuer);

        _ = await Should.ThrowAsync<ArgumentException>(() => client.PurgeAsync(WorkerBase, "../alice", ct)).ConfigureAwait(true);

        handler.Method.ShouldBeNull();
    }

    private static WorkerTokenIssuer Create(CloudTestKeys keys, TimeProvider clock) =>
        new(Microsoft.Extensions.Options.Options.Create(new CloudOptions { Worker = { SigningKeyPem = keys.SigningKeyPem } }), clock);

    /// <summary>Answers every request with one status and keeps what the client sent.</summary>
    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? Target { get; private set; }

        public string? Scheme { get; private set; }

        public string? Parameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Target = request.RequestUri;
            Scheme = request.Headers.Authorization?.Scheme;
            Parameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
