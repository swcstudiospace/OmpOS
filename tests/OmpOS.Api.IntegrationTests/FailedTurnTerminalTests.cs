using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Contracts.Sessions;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.InMemory;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// A failed turn ends its stream on exactly one error event and never appends a completed event after it,
/// whether or not the harness also reports a completion with the error reason.
/// </summary>
public sealed class FailedTurnTerminalTests
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies a failed turn streams one error as its final event and no completed event.</summary>
    /// <param name="workflowsEnabled">The Workflows:Enabled value; false selects the legacy in-process path.</param>
    /// <param name="trailingErrorCompletion">Whether the harness also reports <c>CompletedEvent("error")</c> after its error.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task FailedTurn_StreamEndsOnErrorWithNoCompleted(bool workflowsEnabled, bool trailingErrorCompletion)
    {
        var steps = new List<InMemoryStep> { new EmitStep(new ErrorEvent("boom", Retryable: false)) };
        if (trailingErrorCompletion)
        {
            steps.Add(new EmitStep(new CompletedEvent("error")));
        }

        var script = new InMemoryScript { Steps = steps };
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Workflows:Enabled"] = workflowsEnabled ? "true" : "false" };
        using var factory = new OmpApiFactory(config, configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script)));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Failed, SettleTimeout, testToken);

        var turn = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 50, testToken);
        turn.Count(static item => string.Equals(item.EventType, "completed", StringComparison.Ordinal)).ShouldBe(0, "a failed turn carries no completed event");
        turn.Count(static item => string.Equals(item.EventType, "error", StringComparison.Ordinal)).ShouldBe(1, "a failed turn carries exactly one error event");
        turn[^1].EventType.ShouldBe("error", "the error event is the turn's last event");
    }
}
