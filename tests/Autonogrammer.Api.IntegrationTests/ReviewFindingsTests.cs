using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Api.Sse;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Final-review findings F-01 through F-09: settled-stream close, global rate limits,
/// resume-position validation, and the owner id cap.
/// </summary>
public sealed class ReviewFindingsTests
{
    /// <summary>Verifies a resume at the terminal Seq of a settled session closes promptly with zero items and no leftover signal.</summary>
    [Fact]
    public async Task Events_AtTerminalSeqOnSettledSession_ClosesWithZeroItems()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"finish me\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var completed = await SessionClient.WaitForStatusAsync(client, id, Contracts.Sessions.SessionStatus.Completed, TimeSpan.FromSeconds(15), testToken).ConfigureAwait(true);
        var terminal = completed.Events.Max(static evt => evt.Seq).ToString(CultureInfo.InvariantCulture);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, testToken);
        var items = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 50, linked.Token, terminal).ConfigureAwait(true);
        items.ShouldBeEmpty("no events exist past the terminal Seq, so the stream must close");

        var streamer = factory.Services.GetRequiredService<SessionEventStreamer>();
        streamer.HasSignal(id).ShouldBeFalse("a settled stream must not leave a wake-up signal behind");
    }

    /// <summary>Verifies the prompts bucket is global: a second session's prompt is rejected once the shared budget is spent.</summary>
    [Fact]
    public async Task Prompt_GlobalBucket_SecondSessionOverBudgetReturns429()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:RateLimit:PromptsPerMinute"] = "1" });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var first = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);
        var second = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var allowed = await SessionClient.PostPromptAsync(client, first, "{\"prompt\":\"one\"}", testToken).ConfigureAwait(true);
        allowed.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using var rejected = await SessionClient.PostPromptAsync(client, second, "{\"prompt\":\"two\"}", testToken).ConfigureAwait(true);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
    }

    /// <summary>Verifies session creation over budget returns 429 with Retry-After.</summary>
    [Fact]
    public async Task Create_OverBudget_Returns429WithRetryAfter()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:RateLimit:SessionsPerMinute"] = "2" });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var first = await PostSessionAsync(client, "{}", testToken).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var second = await PostSessionAsync(client, "{}", testToken).ConfigureAwait(true);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var rejected = await PostSessionAsync(client, "{}", testToken).ConfigureAwait(true);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
    }

    /// <summary>Verifies negative resume positions return 400 for both the query and header forms.</summary>
    [Theory]
    [InlineData("/sessions/{0}/events?since=-5", null)]
    [InlineData("/sessions/{0}/events", "-3")]
    public async Task Events_NegativeResume_Returns400(string pathTemplate, string? lastEventId)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(string.Format(CultureInfo.InvariantCulture, pathTemplate, id), UriKind.Relative));
        if (lastEventId is not null)
        {
            request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);
        }

        using var response = await client.SendAsync(request, testToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Verifies a resume past the end of time closes promptly instead of replaying the log.</summary>
    [Fact]
    public async Task Events_MaxValueResume_ClosesWithZeroItems()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"finish me\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await SessionClient.WaitForStatusAsync(client, id, Contracts.Sessions.SessionStatus.Completed, TimeSpan.FromSeconds(15), testToken).ConfigureAwait(true);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, testToken);
        var items = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events?since={long.MaxValue}", 50, linked.Token).ConfigureAwait(true);
        items.ShouldBeEmpty("afterSeq at long.MaxValue must not wrap and replay the log");
    }

    /// <summary>Verifies a body owner id is refused with 400 at every length, so the owner can never be taken from the request.</summary>
    [Theory]
    [InlineData(128)]
    [InlineData(129)]
    public async Task Create_OwnerIdInBody_IsRefusedAtEveryLength(int length)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await PostSessionAsync(client, $"{{\"ownerId\":\"{new string('o', length)}\"}}", testToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<HttpResponseMessage> PostSessionAsync(HttpClient client, string json, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(json);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri("/sessions", UriKind.Relative), content, testToken).ConfigureAwait(false);
    }
}
