using System.Net;
using OmpOS.Api.Harness;
using OmpOS.Contracts.Sessions;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.InMemory;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// A workflow run whose turn failed logs the failure with the status the session settled with, never a completed prompt.
/// The workflow itself completes (the activities settle the turn and return), so the completion log must not follow it.
/// </summary>
public sealed class FailedTurnRunLogTests
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies a failed turn on the workflow path logs a run failure and no completed prompt.</summary>
    [Fact]
    public async Task FailedTurn_WorkflowRunLogsFailureNotCompletion()
    {
        var script = new InMemoryScript
        {
            Steps = new List<InMemoryStep> { new EmitStep(new ErrorEvent("boom", Retryable: false)) },
        };
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Workflows:Enabled"] = "true" };
        var provider = new CapturingLoggerProvider();
        using var factory = new OmpApiFactory(
            config,
            configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script)),
            loggerProvider: provider);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Failed, SettleTimeout, testToken);

        await WaitForRunOutcomeLogAsync(provider, testToken);
        provider.Messages.Any(static line => line.Contains("Prompt run failed for session", StringComparison.Ordinal)).ShouldBeTrue();
        provider.Messages.Any(static line => line.Contains("Prompt completed for session", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>Waits until the background run has logged its outcome, so the assertions see the final log.</summary>
    private static async Task WaitForRunOutcomeLogAsync(CapturingLoggerProvider provider, CancellationToken cancellationToken)
    {
        var attempts = (int)(SettleTimeout.TotalMilliseconds / 25);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (provider.Messages.Any(static line =>
                    line.Contains("Prompt run failed for session", StringComparison.Ordinal)
                    || line.Contains("Prompt completed for session", StringComparison.Ordinal)))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }
    }
}
