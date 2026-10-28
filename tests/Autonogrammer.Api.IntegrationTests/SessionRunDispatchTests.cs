using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Verifies a prompt runs to completion through the in-process direct run path: the scripted deltas stream through the run,
/// the admitted prompt is recorded once, and the run settles on its completed event.
/// </summary>
public sealed class SessionRunDispatchTests
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies a prompt on the direct path runs to completion and settles the session as completed.</summary>
    [Fact]
    public async Task Prompt_RunsToCompletionThroughDirectPath()
    {
        using var factory = new ApiFactory(
            configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(ScriptedHarnessFactory.DeltaScript(2))));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var settled = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);

        settled.Messages.Count.ShouldBe(1, "the admitted prompt is recorded once");
        settled.Events.Select(static evt => evt.Payload).OfType<TextDeltaPayload>().Count().ShouldBe(2, "both scripted deltas stream through the run");
        settled.Events[^1].Payload.ShouldBeOfType<CompletedPayload>("the run ends on its completed event");
    }
}
