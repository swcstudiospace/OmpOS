using System.Net;
using Autonogrammer.Api.Cloud;
using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>The life of a cloud agent against the fake environment client and the in-process worker: provisioning, a run on the worker, resuming the conversation, idling, stopping, waking, cancelling and deleting.</summary>
public sealed class CloudAgentLifecycleTests
{
    /// <summary>A created agent is accepted as provisioning, reaches running through the environment client, and exposes no provider identifier.</summary>
    [Fact]
    public async Task Create_ProvisionsAWorkerAndReachesRunning()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        var created = await host.CreateAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = created.GetProperty("id").GetString()!;
        created.GetProperty("status").GetString().ShouldBe("provisioning");
        created.GetProperty("branch").GetString().ShouldBe("autonogrammer/cloud/" + id);
        created.GetProperty("sessionId").GetString().ShouldStartWith("cloud-", Case.Sensitive);
        created.GetProperty("repositories")[0].GetProperty("url").GetString().ShouldBe(CloudApiHost.RepoUrl);
        created.GetProperty("idleMinutes").GetInt32().ShouldBe(15);

        var running = await host.WaitForStatusAsync(id, "running", cancellationToken: ct).ConfigureAwait(true);

        host.Environment.Fake.ContainersCreated.ShouldBe(1);
        host.Environment.Fake.VolumesCreated.ShouldBe(1);
        host.Environment.Fake.MountPathOf("ctr-1").ShouldBe("/data");
        var workerRef = running.GetProperty("workerRef").GetString()!;
        workerRef.ShouldStartWith("wrk_", Case.Sensitive);
        running.GetRawText().ShouldNotContain("ctr-1", Case.Sensitive);
        running.GetRawText().ShouldNotContain("vol-1", Case.Sensitive);
    }

    /// <summary>A prompt runs on the worker under a fresh ES256 token for the session, and its output reaches the session log; the run never touches the local harness.</summary>
    [Fact]
    public async Task Prompt_RunsOnTheWorkerWithASessionToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var sessionId = agent.GetProperty("sessionId").GetString()!;

        using var accepted = await host.PromptAsync(id, "add a readme", cancellationToken: ct).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var session = await host.WaitForSessionAsync(sessionId, SessionStatus.Completed, ct).ConfigureAwait(true);

        host.Worker.Prompts.ShouldHaveSingleItem().ShouldBe("add a readme");
        session.Events.Select(static evt => evt.Payload).OfType<TextDeltaPayload>().Select(static payload => payload.Delta).ToArray().ShouldBe(["echo: add a readme"]);
        session.Events.Any(static evt => evt.Payload is CompletedPayload).ShouldBeTrue();
        host.Worker.Authorizations.ShouldHaveSingleItem().ShouldStartWith("Bearer ", Case.Sensitive);
        var claims = host.Worker.TokenClaims.ShouldHaveSingleItem();
        claims["sub"].ShouldBe(sessionId);
        claims["aud"].ToString().ShouldBe("autonogrammer-worker");
        claims["iss"].ToString().ShouldBe("autonogrammer-api");
        (Convert.ToInt64(claims["exp"], System.Globalization.CultureInfo.InvariantCulture) - Convert.ToInt64(claims["iat"], System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBeLessThanOrEqualTo(15 * 60);
    }

    /// <summary>The event stream of an agent carries exactly the event kinds and ids the session log holds, as <c>GET /sessions/{id}/events</c> would.</summary>
    [Fact]
    public async Task EventStream_CarriesTheSessionStreamKinds()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var sessionId = agent.GetProperty("sessionId").GetString()!;
        using var accepted = await host.PromptAsync(id, "hello", cancellationToken: ct).ConfigureAwait(true);
        var session = await host.WaitForSessionAsync(sessionId, SessionStatus.Completed, ct).ConfigureAwait(true);

        using var client = host.Client();
        var items = await SessionClient.ReadStreamAsync(client, $"/cloud/agents/{id}/events", session.Events.Count, ct).ConfigureAwait(true);

        items.Select(static item => item.EventType).ShouldBe(session.Events.Select(static evt => Autonogrammer.Api.Sse.StreamEventKind.Of(evt.Payload)).ToArray());
        items.Select(static item => item.EventId).ShouldBe(session.Events.Select(static evt => evt.Seq.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        items.Select(static item => item.EventType).ShouldContain("prompt_accepted", StringComparer.Ordinal);
        items.Select(static item => item.EventType).ShouldContain("text_delta", StringComparer.Ordinal);
        items.Select(static item => item.EventType).ShouldContain("completed", StringComparer.Ordinal);
    }

    /// <summary>A follow-up prompt resumes the engine session with session/load instead of starting a new conversation.</summary>
    [Fact]
    public async Task SecondPrompt_ResumesTheEngineSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var sessionId = agent.GetProperty("sessionId").GetString()!;

        using var first = await host.PromptAsync(id, "one", cancellationToken: ct).ConfigureAwait(true);
        _ = await host.WaitForSessionAsync(sessionId, SessionStatus.Completed, ct).ConfigureAwait(true);
        using var second = await host.PromptAsync(id, "two", cancellationToken: ct).ConfigureAwait(true);
        second.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await WaitForPromptsAsync(host, 2, ct).ConfigureAwait(true);

        host.Worker.NewSessions.ShouldHaveSingleItem().ShouldBe("acp-1");
        host.Worker.LoadedSessions.ShouldHaveSingleItem().ShouldBe("acp-1");
        host.Worker.Authorizations.Count.ShouldBe(2);
        host.Worker.Authorizations.Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
    }

    /// <summary>When the worker no longer has the engine session, the run starts a new one and the agent remembers the new identifier.</summary>
    [Fact]
    public async Task ResumeRefused_StartsANewEngineSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var sessionId = agent.GetProperty("sessionId").GetString()!;
        using var first = await host.PromptAsync(id, "one", cancellationToken: ct).ConfigureAwait(true);
        _ = await host.WaitForSessionAsync(sessionId, SessionStatus.Completed, ct).ConfigureAwait(true);

        host.Worker.RejectLoad = true;
        using var second = await host.PromptAsync(id, "two", cancellationToken: ct).ConfigureAwait(true);
        await WaitForPromptsAsync(host, 2, ct).ConfigureAwait(true);

        host.Worker.NewSessions.Count.ShouldBe(2);
        var record = await host.Services.GetRequiredService<ICloudAgentStore>().GetAsync(id, ct).ConfigureAwait(true);
        record.ShouldNotBeNull().NativeSessionId.ShouldBe("acp-2");
    }

    /// <summary>An agent with no activity for its idle window goes idle: the provider is asked to idle the worker and the awake seconds are counted.</summary>
    [Fact]
    public async Task IdleSweep_IdlesAnInactiveAgentAndCountsComputeSeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;

        host.Clock.Advance(TimeSpan.FromMinutes(14));
        (await host.SweepAsync(ct).ConfigureAwait(true)).Idled.ShouldBe(0);
        host.Clock.Advance(TimeSpan.FromMinutes(2));
        var swept = await host.SweepAsync(ct).ConfigureAwait(true);

        swept.Idled.ShouldBe(1);
        var idle = await host.WaitForStatusAsync(id, "idle", cancellationToken: ct).ConfigureAwait(true);
        idle.GetProperty("idleAt").GetString().ShouldNotBeNull();
        idle.GetProperty("computeSeconds").GetDouble().ShouldBe(16 * 60, 1.0);
        host.Environment.Fake.IdleCalls.ShouldBe(1);
    }

    /// <summary>An agent with a run in flight is never put to idle, however long it was since it last did anything.</summary>
    [Fact]
    public async Task IdleSweep_SkipsAnAgentWhoseRunIsInFlight()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Worker.HoldPrompts = true;
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        using var accepted = await host.PromptAsync(id, "long task", cancellationToken: ct).ConfigureAwait(true);
        await host.Worker.PromptHeld.WaitAsync(TimeSpan.FromSeconds(15), ct).ConfigureAwait(true);

        host.Clock.Advance(TimeSpan.FromHours(2));
        var swept = await host.SweepAsync(ct).ConfigureAwait(true);

        swept.Idled.ShouldBe(0);
        host.Environment.Fake.IdleCalls.ShouldBe(0);
        await host.Worker.ReleaseHeldPromptAsync(ct).ConfigureAwait(true);
        _ = await host.WaitForSessionAsync(agent.GetProperty("sessionId").GetString()!, SessionStatus.Completed, ct).ConfigureAwait(true);
    }

    /// <summary>A request can ask for a shorter idle window than the default.</summary>
    [Fact]
    public async Task IdleMinutes_FromTheRequestDecideWhenTheSweepIdlesTheAgent()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(CloudApiHost.DefaultCreateBody("\"idleMinutes\":2"), cancellationToken: ct).ConfigureAwait(true);

        host.Clock.Advance(TimeSpan.FromMinutes(3));
        var swept = await host.SweepAsync(ct).ConfigureAwait(true);

        swept.Idled.ShouldBe(1);
        agent.GetProperty("idleMinutes").GetInt32().ShouldBe(2);
    }

    /// <summary>A prompt to an idle agent wakes its worker, runs, and leaves the agent running again.</summary>
    [Fact]
    public async Task Prompt_WakesAnIdleAgent()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var sessionId = agent.GetProperty("sessionId").GetString()!;
        host.Clock.Advance(TimeSpan.FromMinutes(20));
        _ = await host.SweepAsync(ct).ConfigureAwait(true);
        _ = await host.WaitForStatusAsync(id, "idle", cancellationToken: ct).ConfigureAwait(true);
        var awakeBefore = host.Environment.Fake.EnsureAwakeCalls;

        using var accepted = await host.PromptAsync(id, "back again", cancellationToken: ct).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await host.WaitForSessionAsync(sessionId, SessionStatus.Completed, ct).ConfigureAwait(true);

        host.Environment.Fake.EnsureAwakeCalls.ShouldBeGreaterThan(awakeBefore);
        host.Worker.Prompts.ShouldContain("back again", StringComparer.Ordinal);
        _ = await host.WaitForStatusAsync(id, "running", cancellationToken: ct).ConfigureAwait(true);
    }

    /// <summary>Stopping an agent idles its worker and refuses prompts until it is woken; waking brings it back.</summary>
    [Fact]
    public async Task StopAndWake_RoundTrip()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var sessionId = agent.GetProperty("sessionId").GetString()!;

        using var stop = await host.PostAsync($"/cloud/agents/{id}/stop", cancellationToken: ct).ConfigureAwait(true);
        stop.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CloudApiHost.ReadAsync(stop, ct).ConfigureAwait(true)).GetProperty("status").GetString().ShouldBe("stopped");
        host.Environment.Fake.IdleCalls.ShouldBe(1);

        using var refused = await host.PromptAsync(id, "no", cancellationToken: ct).ConfigureAwait(true);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(refused, ct).ConfigureAwait(true)).ShouldBe("agent_stopped");

        using var wake = await host.PostAsync($"/cloud/agents/{id}/wake", cancellationToken: ct).ConfigureAwait(true);
        wake.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await host.WaitForStatusAsync(id, "running", cancellationToken: ct).ConfigureAwait(true);
        using var accepted = await host.PromptAsync(id, "yes", cancellationToken: ct).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await host.WaitForSessionAsync(sessionId, SessionStatus.Completed, ct).ConfigureAwait(true);
    }

    /// <summary>Cancel stops the run in flight: the worker is told with session/cancel and the session settles as cancelled.</summary>
    [Fact]
    public async Task Cancel_StopsTheRunOnTheWorker()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Worker.HoldPrompts = true;
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var sessionId = agent.GetProperty("sessionId").GetString()!;
        using var accepted = await host.PromptAsync(id, "forever", cancellationToken: ct).ConfigureAwait(true);
        await host.Worker.PromptHeld.WaitAsync(TimeSpan.FromSeconds(15), ct).ConfigureAwait(true);

        using var cancel = await host.PostAsync($"/cloud/agents/{id}/cancel", cancellationToken: ct).ConfigureAwait(true);

        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
        _ = await host.WaitForSessionAsync(sessionId, SessionStatus.Cancelled, ct).ConfigureAwait(true);
        host.Worker.Cancels.ShouldNotBeEmpty();
    }

    /// <summary>Cancel with nothing running is a conflict, not a success.</summary>
    [Fact]
    public async Task Cancel_WithNoRunInFlightIsAConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);

        using var cancel = await host.PostAsync($"/cloud/agents/{agent.GetProperty("id").GetString()}/cancel", cancellationToken: ct).ConfigureAwait(true);

        cancel.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(cancel, ct).ConfigureAwait(true)).ShouldBe("no_run_in_flight");
    }

    /// <summary>Deleting an agent removes its worker and volume through the catalog, only with the agent's label, and the agent reads as absent afterwards.</summary>
    [Fact]
    public async Task Delete_RemovesTheWorkerWithItsLabelAndTheRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;

        using var client = host.Client();
        using var deleted = await client.DeleteAsync(new Uri($"/cloud/agents/{id}", UriKind.Relative), ct).ConfigureAwait(true);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var removed = host.Environment.Removed.ShouldHaveSingleItem();
        removed.ContainerId.ShouldBe("ctr-1");
        removed.VolumeId.ShouldBe("vol-1");
        removed.Labels[CloudWorkerLabels.AgentKey].ShouldBe(id);
        using var after = await client.GetAsync(new Uri($"/cloud/agents/{id}", UriKind.Relative), ct).ConfigureAwait(true);
        after.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>A worker at the provider that is not the one recorded for the agent is left alone: the delete is refused and the agent is marked orphaned.</summary>
    [Fact]
    public async Task Delete_RefusesWhenTheProviderWorkerIsNotTheRecordedOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        _ = await host.Services.GetRequiredService<ICloudAgentStore>().UpdateAsync(id, static record => record with { WorkerRef = "some-other-worker" }, ct).ConfigureAwait(true);

        using var client = host.Client();
        using var deleted = await client.DeleteAsync(new Uri($"/cloud/agents/{id}", UriKind.Relative), ct).ConfigureAwait(true);

        deleted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(deleted, ct).ConfigureAwait(true)).ShouldBe("worker_mismatch");
        host.Environment.Removed.ShouldBeEmpty();
        _ = await host.WaitForStatusAsync(id, "orphaned", cancellationToken: ct).ConfigureAwait(true);
    }

    /// <summary>An agent that is still provisioning cannot be deleted: its worker is being created and would be left behind.</summary>
    [Fact]
    public async Task Delete_WhileProvisioningIsAConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Environment.CreateGate = gate;
        var created = await host.CreateAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = created.GetProperty("id").GetString()!;

        using var client = host.Client();
        using var deleted = await client.DeleteAsync(new Uri($"/cloud/agents/{id}", UriKind.Relative), ct).ConfigureAwait(true);

        deleted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(deleted, ct).ConfigureAwait(true)).ShouldBe("agent_provisioning");
        gate.SetResult();
        _ = await host.WaitForStatusAsync(id, "running", cancellationToken: ct).ConfigureAwait(true);
    }

    /// <summary>A provider failure marks the agent failed with a stable code and no provider text; a wake request retries and succeeds.</summary>
    [Fact]
    public async Task ProvisioningFailure_IsRecordedAndAWakeRetries()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Environment.FailCreateTimes = 1;
        var created = await host.CreateAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = created.GetProperty("id").GetString()!;

        var failed = await host.WaitForStatusAsync(id, "failed", cancellationToken: ct).ConfigureAwait(true);

        failed.GetProperty("error").GetProperty("code").GetString().ShouldBe("provider_unreachable");
        failed.GetRawText().ShouldNotContain("The provider is down", Case.Sensitive);
        using var refused = await host.PromptAsync(id, "x", cancellationToken: ct).ConfigureAwait(true);
        (await CloudApiHost.ProblemCodeAsync(refused, ct).ConfigureAwait(true)).ShouldBe("agent_failed");
        using var wake = await host.PostAsync($"/cloud/agents/{id}/wake", cancellationToken: ct).ConfigureAwait(true);
        wake.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await host.WaitForStatusAsync(id, "running", cancellationToken: ct).ConfigureAwait(true);
    }

    /// <summary>A wake that times out at the provider marks the agent failed with wake_timeout.</summary>
    [Fact]
    public async Task WakeTimeout_MarksTheAgentFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        host.Clock.Advance(TimeSpan.FromMinutes(20));
        _ = await host.SweepAsync(ct).ConfigureAwait(true);
        _ = await host.WaitForStatusAsync(id, "idle", cancellationToken: ct).ConfigureAwait(true);
        host.Environment.Fake.FailEnsureAwakeTimes = 1;

        using var accepted = await host.PromptAsync(id, "sleepy", cancellationToken: ct).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await host.WaitForSessionAsync(agent.GetProperty("sessionId").GetString()!, SessionStatus.Failed, ct).ConfigureAwait(true);

        var failed = await host.WaitForStatusAsync(id, "failed", cancellationToken: ct).ConfigureAwait(true);
        failed.GetProperty("error").GetProperty("code").GetString().ShouldBe("wake_timeout");
    }

    /// <summary>An agent stuck in provisioning past the timeout (for example after an API crash) is marked failed by the sweep.</summary>
    [Fact]
    public async Task Sweep_FailsAnAgentStuckInProvisioning()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Environment.CreateGate = gate;
        var created = await host.CreateAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = created.GetProperty("id").GetString()!;

        host.Clock.Advance(TimeSpan.FromMinutes(21));
        var swept = await host.SweepAsync(ct).ConfigureAwait(true);

        swept.TimedOut.ShouldBe(1);
        var failed = await host.WaitForStatusAsync(id, "failed", cancellationToken: ct).ConfigureAwait(true);
        failed.GetProperty("error").GetProperty("code").GetString().ShouldBe("provisioning_timeout");
        gate.SetResult();
    }

    /// <summary>An agent that used its compute budget is stopped by the sweep, and a prompt to it is refused as a limit.</summary>
    [Fact]
    public async Task ComputeBudget_StopsTheAgentAndRefusesPrompts()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cloud:Limits:MaxComputeSecondsPerAgent"] = "600" }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;

        host.Clock.Advance(TimeSpan.FromMinutes(11));
        var swept = await host.SweepAsync(ct).ConfigureAwait(true);

        swept.LimitStopped.ShouldBe(1);
        _ = await host.WaitForStatusAsync(id, "limit_reached", cancellationToken: ct).ConfigureAwait(true);
        using var refused = await host.PromptAsync(id, "more", cancellationToken: ct).ConfigureAwait(true);
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await CloudApiHost.ProblemCodeAsync(refused, ct).ConfigureAwait(true)).ShouldBe("compute_limit_reached");
    }

    private static async Task WaitForPromptsAsync(CloudApiHost host, int count, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(15));
        while (host.Worker.Prompts.Count < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), limit.Token).ConfigureAwait(false);
        }

        // The prompt is answered after it is received: wait for the session to settle again.
        var repository = host.Services.GetRequiredService<ISessionRepository>();
        var sessions = host.Services.GetRequiredService<ICloudAgentStore>();
        var agents = await sessions.ListAllAsync(limit.Token).ConfigureAwait(false);
        foreach (var agent in agents)
        {
            while ((await repository.GetAsync(agent.SessionId, limit.Token).ConfigureAwait(false))?.Status is SessionStatus.Running or SessionStatus.AwaitingToolApproval)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), limit.Token).ConfigureAwait(false);
            }
        }
    }
}
