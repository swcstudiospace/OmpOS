using System.Net;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// A failed turn ends its stream on exactly one error event and never appends a completed event after it,
/// whether or not the harness also reports a completion with the error reason.
/// </summary>
public sealed class FailedTurnTerminalTests
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies a failed turn streams one error as its final event and no completed event.</summary>
    /// <param name="trailingErrorCompletion">Whether the harness also reports <c>CompletedEvent("error")</c> after its error.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedTurn_StreamEndsOnErrorWithNoCompleted(bool trailingErrorCompletion)
    {
        var steps = new List<InMemoryStep> { new EmitStep(new ErrorEvent("boom", Retryable: false)) };
        if (trailingErrorCompletion)
        {
            steps.Add(new EmitStep(new CompletedEvent("error")));
        }

        var script = new InMemoryScript { Steps = steps };
        using var factory = new ApiFactory(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script)));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Failed, SettleTimeout, testToken).ConfigureAwait(true);

        var turn = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 50, testToken).ConfigureAwait(true);
        turn.Count(static item => string.Equals(item.EventType, "completed", StringComparison.Ordinal)).ShouldBe(0, "a failed turn carries no completed event");
        turn.Count(static item => string.Equals(item.EventType, "error", StringComparison.Ordinal)).ShouldBe(1, "a failed turn carries exactly one error event");
        turn[^1].EventType.ShouldBe("error", "the error event is the turn's last event");
    }
}
