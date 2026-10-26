using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Contracts.Runs;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using OmpOS.Sandbox;
using OmpOS.Sandbox.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// The hosted run contract against the API's real run machinery (run lease, sandbox provider, harness factory, session log). The
/// harness and sandbox are test doubles that hold the run at a known point; everything between them is the production path.
/// </summary>
public sealed class CloudRunTests
{
    private const string FirstDiffPayload = """{"content":[{"type":"diff","path":"src/Greeting.cs","oldText":"class A;","newText":"class A { }"}]}""";
    private const string SecondDiffPayload = """{"content":[{"type":"diff","path":"src/Created.cs","newText":"class C;"}]}""";
    private const string FailedDiffPayload = """{"content":[{"type":"diff","path":"src/Never.cs","oldText":"a","newText":"b"}]}""";
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A run is Queued until its harness starts the prompt, Running while it executes, and Completed once the turn settles; each state is read through the API.</summary>
    [Fact]
    public async Task CloudRunMovesThroughQueuedRunningCompleted()
    {
        var sandbox = new CloudRunSandboxProvider(new FakeSandboxProvider());
        var harness = new CloudRunHarnessFactory([new TextDeltaEvent("hello")], gated: true);
        using var factory = CreateFactory(harness, sandbox);
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);

        var accepted = await CloudRunClient.CreateRunAsync(client, sessionId, "say hello", Token).ConfigureAwait(true);
        accepted.Status.ShouldBe(RunStatus.Queued);
        accepted.SessionId.ShouldBe(sessionId);

        // The run is held at sandbox provisioning: accepted, and its harness has not started.
        await CloudRunClient.AwaitAsync(sandbox.Entered, Token).ConfigureAwait(true);
        var queued = await CloudRunClient.GetRunAsync(client, sessionId, accepted.Id, Token).ConfigureAwait(true);
        queued.Status.ShouldBe(RunStatus.Queued);
        queued.CompletedAt.ShouldBeNull();

        // Provisioning finishes and the harness starts the prompt, then waits at its gate.
        sandbox.Release();
        await CloudRunClient.AwaitAsync(harness.Entered, Token).ConfigureAwait(true);
        var running = await CloudRunClient.GetRunAsync(client, sessionId, accepted.Id, Token).ConfigureAwait(true);
        running.Status.ShouldBe(RunStatus.Running);
        running.CompletedAt.ShouldBeNull();

        harness.Release();
        _ = await SessionClient.WaitForStatusAsync(client, sessionId, SessionStatus.Completed, SettleTimeout, Token).ConfigureAwait(true);
        var completed = await CloudRunClient.GetRunAsync(client, sessionId, accepted.Id, Token).ConfigureAwait(true);
        completed.Status.ShouldBe(RunStatus.Completed);
        completed.CompletedAt.ShouldNotBeNull();
        completed.Id.ShouldBe(accepted.Id);
    }

    /// <summary>An unfinished run reports its status and no output: the output body has no transcript and no changes until the run settles.</summary>
    [Fact]
    public async Task AnUnfinishedCloudRunReportsItsStatusAndNoOutput()
    {
        var sandbox = new CloudRunSandboxProvider(new FakeSandboxProvider());
        var harness = new CloudRunHarnessFactory([new TextDeltaEvent("hello")], gated: true);
        using var factory = CreateFactory(harness, sandbox);
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);
        var run = await CloudRunClient.CreateRunAsync(client, sessionId, "say hello", Token).ConfigureAwait(true);
        await CloudRunClient.AwaitAsync(sandbox.Entered, Token).ConfigureAwait(true);

        using var queuedResponse = await client.GetAsync(new Uri($"/sessions/{sessionId}/runs/{run.Id}/output", UriKind.Relative), Token).ConfigureAwait(true);
        queuedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var queuedBody = JsonDocument.Parse(await queuedResponse.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        queuedBody.RootElement.GetProperty("status").GetString().ShouldBe("Queued");
        queuedBody.RootElement.TryGetProperty("transcript", out _).ShouldBeFalse();
        queuedBody.RootElement.TryGetProperty("changes", out _).ShouldBeFalse();

        sandbox.Release();
        await CloudRunClient.AwaitAsync(harness.Entered, Token).ConfigureAwait(true);
        var running = await CloudRunClient.GetOutputAsync(client, sessionId, run.Id, Token).ConfigureAwait(true);
        running.Status.ShouldBe(RunStatus.Running);
        running.Transcript.ShouldBeNull();
        running.Changes.ShouldBeNull();

        harness.Release();
        _ = await SessionClient.WaitForStatusAsync(client, sessionId, SessionStatus.Completed, SettleTimeout, Token).ConfigureAwait(true);
    }

    /// <summary>
    /// A completed run returns its own transcript and the file changes its harness reported. A later run on the same session does not
    /// leak into it, a call that reports its diff twice yields it once, and a failed call's diff is left out.
    /// </summary>
    [Fact]
    public async Task CompletedCloudRunReturnsDiffAndTranscript()
    {
        var harness = new CloudRunHarnessFactory(
            [
                new TextDeltaEvent("wrote the files"),
                new ToolCallUpdateEvent("call-1", "in_progress", FirstDiffPayload),
                new ToolCallUpdateEvent("call-1", "completed", FirstDiffPayload),
                new ToolCallUpdateEvent("call-2", "completed", SecondDiffPayload),
                new ToolCallUpdateEvent("call-3", "failed", FailedDiffPayload),
            ],
            gated: false);
        using var factory = CreateFactory(harness, new FakeSandboxProvider());
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);

        var first = await CloudRunClient.CreateRunAsync(client, sessionId, "add the files", Token).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(client, sessionId, SessionStatus.Completed, SettleTimeout, Token).ConfigureAwait(true);
        var output = await CloudRunClient.GetOutputAsync(client, sessionId, first.Id, Token).ConfigureAwait(true);

        output.Status.ShouldBe(RunStatus.Completed);
        var transcript = output.Transcript.ShouldNotBeNull();
        transcript.Messages.ShouldHaveSingleItem().Content.ShouldBe("add the files");
        transcript.Events.Select(static evt => evt.Payload).OfType<TextDeltaPayload>().ShouldHaveSingleItem().Delta.ShouldBe("wrote the files");
        transcript.Events[^1].Payload.ShouldBeOfType<CompletedPayload>();
        var changes = output.Changes.ShouldNotBeNull();
        changes.Count.ShouldBe(2);
        changes[0].Path.ShouldBe("src/Greeting.cs");
        changes[0].OldText.ShouldBe("class A;");
        changes[0].NewText.ShouldBe("class A { }");
        changes[1].Path.ShouldBe("src/Created.cs");
        changes[1].OldText.ShouldBeNull();
        changes[1].NewText.ShouldBe("class C;");

        // A second run on the same session owns its own turn, and the first run's output does not change.
        var second = await CloudRunClient.CreateRunAsync(client, sessionId, "again", Token).ConfigureAwait(true);
        await WaitForRunAsync(client, sessionId, second.Id, RunStatus.Completed).ConfigureAwait(true);
        var secondOutput = await CloudRunClient.GetOutputAsync(client, sessionId, second.Id, Token).ConfigureAwait(true);
        secondOutput.Transcript.ShouldNotBeNull().Messages.ShouldHaveSingleItem().Content.ShouldBe("again");
        var firstAgain = await CloudRunClient.GetOutputAsync(client, sessionId, first.Id, Token).ConfigureAwait(true);
        firstAgain.Status.ShouldBe(RunStatus.Completed);
        firstAgain.Transcript.ShouldNotBeNull().Messages.ShouldHaveSingleItem().Content.ShouldBe("add the files");
        firstAgain.Changes.ShouldNotBeNull().Count.ShouldBe(2);
    }

    /// <summary>A harness that reports no file edit yields the transcript with an empty change list.</summary>
    [Fact]
    public async Task ACompletedCloudRunWithoutFileEditsReturnsTheTranscriptOnly()
    {
        var harness = new CloudRunHarnessFactory([new TextDeltaEvent("nothing to change")], gated: false);
        using var factory = CreateFactory(harness, new FakeSandboxProvider());
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);
        var run = await CloudRunClient.CreateRunAsync(client, sessionId, "look around", Token).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(client, sessionId, SessionStatus.Completed, SettleTimeout, Token).ConfigureAwait(true);

        var output = await CloudRunClient.GetOutputAsync(client, sessionId, run.Id, Token).ConfigureAwait(true);

        output.Status.ShouldBe(RunStatus.Completed);
        output.Transcript.ShouldNotBeNull().Events.ShouldNotBeEmpty();
        output.Changes.ShouldNotBeNull().ShouldBeEmpty();
    }

    /// <summary>Cancel stops a running run, which then reports Cancelled with its partial transcript; a second cancel returns the same Cancelled run.</summary>
    [Fact]
    public async Task CancellingARunningCloudRunSettlesItAsCancelled()
    {
        var harness = new CloudRunHarnessFactory([new TextDeltaEvent("never emitted")], gated: true);
        using var factory = CreateFactory(harness, new FakeSandboxProvider());
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);
        var run = await CloudRunClient.CreateRunAsync(client, sessionId, "long task", Token).ConfigureAwait(true);
        await CloudRunClient.AwaitAsync(harness.Entered, Token).ConfigureAwait(true);

        using var cancel = await CloudRunClient.PostCancelAsync(client, sessionId, run.Id, Token).ConfigureAwait(true);
        cancel.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, sessionId, SessionStatus.Cancelled, SettleTimeout, Token).ConfigureAwait(true);

        var cancelled = await CloudRunClient.GetRunAsync(client, sessionId, run.Id, Token).ConfigureAwait(true);
        cancelled.Status.ShouldBe(RunStatus.Cancelled);
        cancelled.CompletedAt.ShouldNotBeNull();
        var output = await CloudRunClient.GetOutputAsync(client, sessionId, run.Id, Token).ConfigureAwait(true);
        output.Status.ShouldBe(RunStatus.Cancelled);
        output.Transcript.ShouldNotBeNull().Messages.ShouldHaveSingleItem().Content.ShouldBe("long task");
        output.Changes.ShouldNotBeNull().ShouldBeEmpty();

        using var again = await CloudRunClient.PostCancelAsync(client, sessionId, run.Id, Token).ConfigureAwait(true);
        again.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await CloudRunClient.ReadRunAsync(again, Token).ConfigureAwait(true)).Status.ShouldBe(RunStatus.Cancelled);
    }

    /// <summary>A run that is still queued can be cancelled before its harness starts, and the harness never runs.</summary>
    [Fact]
    public async Task CancellingAQueuedCloudRunNeverStartsItsHarness()
    {
        var sandbox = new CloudRunSandboxProvider(new FakeSandboxProvider());
        var harness = new CloudRunHarnessFactory([new TextDeltaEvent("never emitted")], gated: false);
        using var factory = CreateFactory(harness, sandbox);
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);
        var run = await CloudRunClient.CreateRunAsync(client, sessionId, "queued task", Token).ConfigureAwait(true);
        await CloudRunClient.AwaitAsync(sandbox.Entered, Token).ConfigureAwait(true);

        using var cancel = await CloudRunClient.PostCancelAsync(client, sessionId, run.Id, Token).ConfigureAwait(true);
        cancel.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, sessionId, SessionStatus.Cancelled, SettleTimeout, Token).ConfigureAwait(true);

        (await CloudRunClient.GetRunAsync(client, sessionId, run.Id, Token).ConfigureAwait(true)).Status.ShouldBe(RunStatus.Cancelled);
        harness.Entered.IsCompleted.ShouldBeFalse();
    }

    /// <summary>A run that already completed cannot be cancelled: 409, and its status does not change.</summary>
    [Fact]
    public async Task CancellingACompletedCloudRunAnswers409()
    {
        var harness = new CloudRunHarnessFactory([new TextDeltaEvent("done")], gated: false);
        using var factory = CreateFactory(harness, new FakeSandboxProvider());
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);
        var run = await CloudRunClient.CreateRunAsync(client, sessionId, "quick task", Token).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(client, sessionId, SessionStatus.Completed, SettleTimeout, Token).ConfigureAwait(true);

        using var cancel = await CloudRunClient.PostCancelAsync(client, sessionId, run.Id, Token).ConfigureAwait(true);

        cancel.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudRunClient.GetRunAsync(client, sessionId, run.Id, Token).ConfigureAwait(true)).Status.ShouldBe(RunStatus.Completed);
    }

    /// <summary>A second run on a session whose prompt is still in flight answers 409 and creates no run.</summary>
    [Fact]
    public async Task ACloudRunOnASessionWithAPromptInFlightAnswers409()
    {
        var harness = new CloudRunHarnessFactory([new TextDeltaEvent("busy")], gated: true);
        using var factory = CreateFactory(harness, new FakeSandboxProvider());
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);
        _ = await CloudRunClient.CreateRunAsync(client, sessionId, "first", Token).ConfigureAwait(true);
        await CloudRunClient.AwaitAsync(harness.Entered, Token).ConfigureAwait(true);

        using var refused = await CloudRunClient.PostRunAsync(client, sessionId, "second", Token).ConfigureAwait(true);

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var session = await SessionClient.ReadSessionAsync(client, sessionId, Token).ConfigureAwait(true);
        session.Messages.ShouldHaveSingleItem().Content.ShouldBe("first");
        harness.Release();
        _ = await SessionClient.WaitForStatusAsync(client, sessionId, SessionStatus.Completed, SettleTimeout, Token).ConfigureAwait(true);
    }

    /// <summary>An unknown run id, and a run id taken from another session, answer 404 on every run operation.</summary>
    [Fact]
    public async Task AnUnknownCloudRunAnswers404OnEveryRunOperation()
    {
        var harness = new CloudRunHarnessFactory([new TextDeltaEvent("done")], gated: false);
        using var factory = CreateFactory(harness, new FakeSandboxProvider());
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);
        var otherSessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);
        var run = await CloudRunClient.CreateRunAsync(client, sessionId, "mine", Token).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(client, sessionId, SessionStatus.Completed, SettleTimeout, Token).ConfigureAwait(true);

        foreach (var (method, path) in RunOperations(sessionId, "no-such-run").Concat(RunOperations(otherSessionId, run.Id)))
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
            using var response = await client.SendAsync(request, Token).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
        }

        using var noSession = await CloudRunClient.PostRunAsync(client, "no-such-session", "x", Token).ConfigureAwait(true);
        noSession.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Gets the three run operations that address an existing run, as method and relative path.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="runId">The run identifier.</param>
    /// <returns>The operations.</returns>
    internal static IEnumerable<(string Method, string Path)> RunOperations(string sessionId, string runId)
    {
        yield return ("GET", $"/sessions/{sessionId}/runs/{runId}");
        yield return ("GET", $"/sessions/{sessionId}/runs/{runId}/output");
        yield return ("POST", $"/sessions/{sessionId}/runs/{runId}/cancel");
    }

    private static async Task WaitForRunAsync(HttpClient client, string sessionId, string runId, RunStatus status)
    {
        using var timeout = new CancellationTokenSource(SettleTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, Token);
        while (true)
        {
            var run = await CloudRunClient.GetRunAsync(client, sessionId, runId, linked.Token).ConfigureAwait(true);
            if (run.Status == status)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), linked.Token).ConfigureAwait(true);
        }
    }

    private static OmpApiFactory CreateFactory(CloudRunHarnessFactory harness, ISandboxProvider sandbox) =>
        new(configureServices: services =>
        {
            services.AddSingleton<IHarnessSessionFactory>(harness);
            services.AddSingleton(sandbox);
        });
}
