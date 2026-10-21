using System.Net;
using OmpOS.Contracts.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// One completed event per turn: a turn's stream ends on its single completed event, and a client
/// resuming after that event receives nothing further. Covers the direct in-process path.
/// </summary>
public sealed class CompletionOncePerTurnTests
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies a turn on the default direct path streams exactly one completed event, as its final event.</summary>
    [Fact]
    public async Task Turn_StreamsOneCompletedAsFinalEvent()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Workflows:Enabled"] = "false" };
        using var factory = new OmpApiFactory(config);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken);

        var turn = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 50, testToken);
        turn.Count(static item => string.Equals(item.EventType, "completed", StringComparison.Ordinal)).ShouldBe(1, "a turn carries exactly one completed event");
        turn[^1].EventType.ShouldBe("completed", "the completed event is the turn's last event");

        var afterCompletion = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 50, testToken, turn[^1].EventId);
        afterCompletion.ShouldBeEmpty("a client resuming after the completed event has nothing left to read");
    }
}
