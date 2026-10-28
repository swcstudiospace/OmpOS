using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Api.Endpoints;
using Autonogrammer.Api.Idempotency;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Coordination;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Verifies the prompt Idempotency-Key contract. A completed key replays its stored 202 body without running the prompt again.
/// A key reused for a different body, or retried while its first request is still pending, is a 409 ProblemDetails.
/// A malformed key is a 400 ProblemDetails.
/// </summary>
public sealed class PromptIdempotencyTests
{
    private const string Key = "prompt-key-1";
    private const string HelloBody = "{\"prompt\":\"hello\"}";
    private const string OtherBody = "{\"prompt\":\"other\"}";
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Gets header values that are not 1 to 128 visible ASCII characters.</summary>
    public static TheoryData<string> MalformedKeys => new() { "has space", new string('k', 129) };

    /// <summary>Verifies a repeated request with the same key and body replays the stored 202 body and runs the prompt once.</summary>
    [Fact]
    public async Task SameKeyAndBody_ReplaysStoredAcceptedBodyAndRunsOnce()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var first = await SessionClient.PostPromptAsync(client, id, HelloBody, testToken, idempotencyKey: Key).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var firstBody = await first.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);

        using var replay = await SessionClient.PostPromptAsync(client, id, HelloBody, testToken, idempotencyKey: Key).ConfigureAwait(true);
        replay.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var replayBody = await replay.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);

        JsonNode.DeepEquals(JsonNode.Parse(replayBody), JsonNode.Parse(firstBody)).ShouldBeTrue("the replay returns the stored 202 body");
        firstBody.ShouldContain(id);

        var settled = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);
        settled.Messages.Count.ShouldBe(1, "the replayed request is neither admitted nor run a second time");
    }

    /// <summary>Verifies a key reused for a different prompt body is refused with 409 idempotency-key-reused and admits nothing.</summary>
    [Fact]
    public async Task SameKeyDifferentBody_Returns409KeyReused()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var first = await SessionClient.PostPromptAsync(client, id, HelloBody, testToken, idempotencyKey: Key).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);

        using var reused = await SessionClient.PostPromptAsync(client, id, OtherBody, testToken, idempotencyKey: Key).ConfigureAwait(true);

        reused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await AssertProblemTypeAsync(reused, "/problems/idempotency-key-reused", testToken).ConfigureAwait(true);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);
        session.Messages.Count.ShouldBe(1, "the conflicting request is not admitted");
    }

    /// <summary>Verifies a request retried with a key whose first request is still pending is refused with 409 idempotency-in-progress.</summary>
    [Fact]
    public async Task PendingClaimWithSameBody_Returns409InProgress()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        var idempotency = factory.Services.GetRequiredService<IIdempotencyStore>();
        var fingerprint = PromptFingerprint.Of(new PromptRequest { Prompt = "hello" });
        var claim = await idempotency.ClaimAsync(id, Key, fingerprint, testToken).ConfigureAwait(true);
        claim.Outcome.ShouldBe(IdempotencyOutcome.Claimed, "the test holds the pending claim");

        using var response = await SessionClient.PostPromptAsync(client, id, HelloBody, testToken, idempotencyKey: Key).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await AssertProblemTypeAsync(response, "/problems/idempotency-in-progress", testToken).ConfigureAwait(true);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);
        session.Messages.ShouldBeEmpty("the pending request is not admitted");
        session.Events.ShouldBeEmpty("no event is appended while the claim is pending");
    }

    /// <summary>Verifies a key that is not 1 to 128 visible ASCII characters is refused with 400 and admits nothing.</summary>
    /// <param name="key">The malformed header value.</param>
    [Theory]
    [MemberData(nameof(MalformedKeys))]
    public async Task MalformedKey_Returns400AndAdmitsNothing(string key)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var response = await SessionClient.PostPromptAsync(client, id, HelloBody, testToken, idempotencyKey: key).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);
        session.Messages.ShouldBeEmpty("a rejected key admits no prompt");
        session.Events.ShouldBeEmpty("a rejected key appends no event");
    }

    /// <summary>Creates a host whose harness completes every prompt with one scripted delta.</summary>
    /// <returns>The test host factory.</returns>
    private static ApiFactory CreateFactory() =>
        new(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(ScriptedHarnessFactory.DeltaScript(1))));

    /// <summary>Asserts a ProblemDetails response carries the expected problem type.</summary>
    /// <param name="response">The response to inspect.</param>
    /// <param name="typeSuffix">The required suffix of the problem type.</param>
    /// <param name="testToken">The test cancellation token.</param>
    private static async Task AssertProblemTypeAsync(HttpResponseMessage response, string typeSuffix, CancellationToken testToken)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(true));
        var type = document.RootElement.GetProperty("type").GetString() ?? string.Empty;
        type.ShouldEndWith(typeSuffix);
    }
}
