using System.Text.Json;
using OmpOS.Harness.AgentLoop.Hooks;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// The hooks service end to end with a runner that starts nothing: consent before any spawn, what an edit invalidates, workspace trust, quarantine,
/// the run log and its events, the bridge lifecycle and restart, and the global switch.
/// </summary>
public sealed class HooksServiceTests
{
    private static JsonElement ShellPayload(string project) => HookPayloads.BeforeShellExecution("git push origin main", project, sandbox: false);

    private static HookInfo Guard(HooksOverview overview) => overview.Hooks.Single(static h => string.Equals(h.Hook.EventName, "beforeShellExecution", StringComparison.Ordinal));

    /// <summary>A guarding hook never starts without consent: the action is denied with the reason, nothing is spawned, approval lets it run, and withdrawing the approval blocks it again.</summary>
    [Fact]
    public async Task AGuardingHookDoesNotStartWithoutConsentAndRunsAfterApproval()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        var blocked = await session.DispatchAsync(HookEvent.BeforeShellExecution, ShellPayload(rig.Project), ct).ConfigureAwait(true);

        blocked.IsDenied.ShouldBeTrue();
        blocked.Cause.ShouldBe(HookDecisionCause.ConsentMissing);
        blocked.Records.Single().Status.ShouldBe(HookRunStatus.AwaitingConsent);
        rig.Runner.SpawnCount.ShouldBe(0);

        var overview = await rig.OverviewAsync(ct).ConfigureAwait(true);
        var guard = Guard(overview);
        guard.IsConsented.ShouldBeFalse();
        await rig.Service.ApproveAsync(guard.Hook, ct).ConfigureAwait(true);

        var allowed = await session.DispatchAsync(HookEvent.BeforeShellExecution, ShellPayload(rig.Project), ct).ConfigureAwait(true);

        allowed.IsDenied.ShouldBeFalse();
        allowed.Verdict.ShouldBe(HookVerdict.Allow);
        rig.Runner.SpawnCount.ShouldBe(1);
        ScriptedHookRunner.InputOf(rig.Runner.Invocations[0]).ShouldContain("\"command\":\"git push origin main\"", Case.Sensitive);

        await rig.Service.RevokeAsync(guard.Hook, ct).ConfigureAwait(true);
        (await session.DispatchAsync(HookEvent.BeforeShellExecution, ShellPayload(rig.Project), ct).ConfigureAwait(true)).IsDenied.ShouldBeTrue();
        rig.Runner.SpawnCount.ShouldBe(1);
    }

    /// <summary>A hook with no consent that does not guard anything is skipped, not denied, and nothing is spawned for it.</summary>
    [Fact]
    public async Task ANonGuardingHookWithoutConsentIsSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        var decision = await session.DispatchAsync(HookEvent.AfterAgentResponse, HookPayloads.AfterAgentResponse("done"), ct).ConfigureAwait(true);

        decision.IsDenied.ShouldBeFalse();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.AwaitingConsent);
        rig.Runner.SpawnCount.ShouldBe(0);
    }

    /// <summary>Editing a consented hook voids the consent, and the overview still holds the version that was approved so the page can show the difference.</summary>
    [Fact]
    public async Task EditingAHookInvalidatesConsentAndKeepsTheApprovedVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        (await rig.OverviewAsync(ct).ConfigureAwait(true)).Hooks.ShouldAllBe(static h => h.IsConsented);

        rig.Files.Set(rig.HooksFile, HooksServiceRig.DefaultHooksJson.Replace("git push\"", "git push --force\"", StringComparison.Ordinal));
        var edited = Guard(await rig.OverviewAsync(ct).ConfigureAwait(true));

        edited.IsConsented.ShouldBeFalse();
        edited.Hook.Matcher.ShouldBe("git push --force");
        var approved = edited.LastApproved.ShouldNotBeNull();
        approved.Matcher.ShouldBe("git push");
        approved.Command.ShouldBe("./guard.sh");
        approved.FailClosed.ShouldBeTrue();
        approved.WorkingDirectory.ShouldBe(".");
    }

    /// <summary>Project hooks are not loaded until the workspace is trusted; trusting lists them without approving any, the trust survives a restart, and an environment can override it either way.</summary>
    [Fact]
    public async Task ProjectHooksAreWithheldUntilTheWorkspaceIsTrusted()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);

        var before = await rig.OverviewAsync(ct, trustProjectHooks: null).ConfigureAwait(true);
        before.Hooks.ShouldBeEmpty();
        before.WithheldSources.Single().Path.ShouldBe(rig.HooksFile);

        await rig.Service.SetWorkspaceTrustedAsync(rig.Project, trusted: true, ct).ConfigureAwait(true);
        var after = await rig.OverviewAsync(ct, trustProjectHooks: null).ConfigureAwait(true);
        after.Hooks.Count.ShouldBe(2);
        after.WithheldSources.ShouldBeEmpty();
        after.Hooks.ShouldAllBe(static h => !h.IsConsented);

        (await rig.OverviewAsync(ct, trustProjectHooks: false).ConfigureAwait(true)).Hooks.ShouldBeEmpty();

        await rig.ReopenAsync(ct).ConfigureAwait(true);
        rig.Service.IsWorkspaceTrusted(rig.Project).ShouldBeTrue();
        (await rig.OverviewAsync(ct, trustProjectHooks: null).ConfigureAwait(true)).Hooks.Count.ShouldBe(2);
    }

    /// <summary>When the runner itself fails, the source is quarantined: its guarding hook then blocks without starting, and lifting the quarantine with a reason lets it run again.</summary>
    [Fact]
    public async Task ARunnerFaultQuarantinesTheSourceUntilItIsLifted()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        rig.Runner.Behavior = static (_, _) => throw new InvalidOperationException("the shell is missing");
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        var faulted = await session.DispatchAsync(HookEvent.BeforeShellExecution, ShellPayload(rig.Project), ct).ConfigureAwait(true);
        faulted.IsDenied.ShouldBeTrue();
        faulted.Cause.ShouldBe(HookDecisionCause.RunnerFault);
        var spawnsAfterFault = rig.Runner.SpawnCount;

        var quarantined = await session.DispatchAsync(HookEvent.BeforeShellExecution, ShellPayload(rig.Project), ct).ConfigureAwait(true);
        quarantined.Cause.ShouldBe(HookDecisionCause.PluginQuarantined);
        rig.Runner.SpawnCount.ShouldBe(spawnsAfterFault);

        var guard = Guard(await rig.OverviewAsync(ct).ConfigureAwait(true));
        guard.Quarantine.ShouldNotBeNull().Reason.ShouldNotBeNullOrWhiteSpace();
        (await rig.Service.LiftQuarantineAsync(guard.Hook.PluginId, "the shell was reinstalled", ct).ConfigureAwait(true)).ShouldBeTrue();
        rig.Runner.Behavior = static (_, _) => Task.FromResult(ScriptedHookRunner.Say("{\"permission\":\"allow\"}"));

        var recovered = await session.DispatchAsync(HookEvent.BeforeShellExecution, ShellPayload(rig.Project), ct).ConfigureAwait(true);
        recovered.IsDenied.ShouldBeFalse();
        Guard(await rig.OverviewAsync(ct).ConfigureAwait(true)).Quarantine.ShouldBeNull();
    }

    /// <summary>Every record is raised as an event, kept for the page, written to the session's run log and remembered as the hook's last run.</summary>
    [Fact]
    public async Task EveryRecordIsPublishedLoggedAndRemembered()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        var raised = new List<HookRunRecord>();
        rig.Service.RunRecorded += (_, e) => raised.Add(e.Record);
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        await session.DispatchAsync(HookEvent.AfterAgentResponse, HookPayloads.AfterAgentResponse("done"), ct).ConfigureAwait(true);

        var record = raised.ShouldHaveSingleItem();
        record.Status.ShouldBe(HookRunStatus.Succeeded);
        record.EventName.ShouldBe("afterAgentResponse");
        rig.Service.RecentRuns(10).ShouldBe([record]);
        session.RecentRuns(10).ShouldBe([record]);
        var logFile = SessionStateLayout.HooksRunLogFile(SessionStateLayout.SessionDirectory(rig.SessionStateRoot, "s1"));
        (await File.ReadAllTextAsync(logFile, ct).ConfigureAwait(true)).ShouldContain(record.HookId, Case.Sensitive);
        var audit = (await rig.OverviewAsync(ct).ConfigureAwait(true)).Hooks.Single(static h => string.Equals(h.Hook.EventName, "afterAgentResponse", StringComparison.Ordinal));
        audit.LastRun.ShouldBe(record);
    }

    /// <summary>The service reports a hook as running while its process runs, and clears it when it finishes.</summary>
    [Fact]
    public async Task AHookIsReportedRunningWhileItsProcessRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Runner.Behavior = async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(ct).ConfigureAwait(false);
            return ScriptedHookRunner.Say("{\"permission\":\"allow\"}");
        };
        var changes = 0;
        rig.Service.RunningChanged += (_, _) => Interlocked.Increment(ref changes);
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        var dispatch = session.DispatchAsync(HookEvent.BeforeShellExecution, ShellPayload(rig.Project), ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);

        var guard = Guard(await rig.OverviewAsync(ct).ConfigureAwait(true));
        rig.Service.RunningHookIds.ShouldBe([guard.Hook.Id]);
        release.SetResult();
        await dispatch.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);

        rig.Service.RunningHookIds.ShouldBeEmpty();
        changes.ShouldBe(2);
    }

    /// <summary>
    /// A session starts a loopback bridge whose endpoint carries the URL, the token and the startup file; the startup file holds event names, never the
    /// token; disposing the session removes the file; a restart gets a fresh token, and consent survives a restart of the application.
    /// </summary>
    [Fact]
    public async Task TheBridgeStartsWithTheSessionStopsWithItAndRestartsWithANewToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: true, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        var startupFile = SessionStateLayout.HooksStartupFile(SessionStateLayout.SessionDirectory(rig.SessionStateRoot, "s1"));

        string firstToken;
        var first = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        try
        {
            var endpoint = first.Endpoint.ShouldNotBeNull();
            endpoint.IsLoadable.ShouldBeTrue();
            endpoint.Environment[HookBridgeEnvironment.Url].ShouldStartWith("http://127.0.0.1:", Case.Sensitive);
            endpoint.Environment[HookBridgeEnvironment.StartupFile].ShouldBe(startupFile);
            firstToken = endpoint.Environment[HookBridgeEnvironment.Token];
            File.Exists(startupFile).ShouldBeTrue();
            var contents = await File.ReadAllTextAsync(startupFile, ct).ConfigureAwait(true);
            contents.ShouldNotContain(firstToken, Case.Sensitive);
            contents.ShouldContain("beforeShellExecution", Case.Sensitive);
            contents.ShouldContain("afterAgentResponse", Case.Sensitive);
            first.Gate.ShouldNotBeNull();
        }
        finally
        {
            await first.DisposeAsync().ConfigureAwait(true);
        }

        File.Exists(startupFile).ShouldBeFalse();

        await rig.ReopenAsync(ct).ConfigureAwait(true);
        var second = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var secondScope = second.ConfigureAwait(true);
        second.Endpoint.ShouldNotBeNull().Environment[HookBridgeEnvironment.Token].ShouldNotBe(firstToken, StringComparer.Ordinal);
        (await rig.OverviewAsync(ct).ConfigureAwait(true)).Hooks.ShouldAllBe(static h => h.IsConsented);
    }

    /// <summary>Without a plugins root no bridge is started and the session carries no endpoint, so only the ACP tap produces events.</summary>
    [Fact]
    public async Task WithoutAPluginsRootThereIsNoEndpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        session.IsActive.ShouldBeTrue();
        session.Endpoint.ShouldBeNull();
        session.Gate.ShouldNotBeNull();
    }

    /// <summary>With Run hooks off a session is inactive: no gate, no endpoint, nothing runs. The setting survives a restart.</summary>
    [Fact]
    public async Task TurningHooksOffGivesAnInactiveSessionAndSurvivesARestart()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: true, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        await rig.Service.SetRunHooksAsync(false, ct).ConfigureAwait(true);

        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            session.IsActive.ShouldBeFalse();
            session.Gate.ShouldBeNull();
            session.Endpoint.ShouldBeNull();
            (await session.DispatchAsync(HookEvent.BeforeShellExecution, ShellPayload(rig.Project), ct).ConfigureAwait(true)).ShouldBe(HookDecision.None);
        }

        rig.Runner.SpawnCount.ShouldBe(0);
        await rig.ReopenAsync(ct).ConfigureAwait(true);
        rig.Service.RunHooks.ShouldBeFalse();
        (await rig.OverviewAsync(ct).ConfigureAwait(true)).RunHooks.ShouldBeFalse();
    }

    /// <summary>A consent file that cannot be read is set aside and the store starts empty, so no hook is assumed consented.</summary>
    [Fact]
    public async Task ACorruptConsentFileIsSetAsideAndNothingIsConsented()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        var consentFile = Path.Combine(rig.StateDirectory, "hooks-consent.json");
        await rig.Service.DisposeAsync().ConfigureAwait(true);
        await File.WriteAllTextAsync(consentFile, "{ this is not json", ct).ConfigureAwait(true);

        await rig.ReopenAsync(ct).ConfigureAwait(true);

        File.Exists(consentFile + ".corrupt").ShouldBeTrue();
        (await rig.OverviewAsync(ct).ConfigureAwait(true)).Hooks.ShouldAllBe(static h => !h.IsConsented);
    }

    /// <summary>Turning a hook off is stored in OmpOS's own state: the hook file is not touched.</summary>
    [Fact]
    public async Task SwitchingAHookOffNeverWritesTheHookFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        var guard = Guard(await rig.OverviewAsync(ct).ConfigureAwait(true));

        await rig.Service.SetHookEnabledAsync(guard.Hook.Id, enabled: false, ct).ConfigureAwait(true);

        Guard(await rig.OverviewAsync(ct).ConfigureAwait(true)).IsEnabled.ShouldBeFalse();
        var bytes = await rig.Files.ReadAsync(rig.HooksFile, int.MaxValue, ct).ConfigureAwait(true);
        System.Text.Encoding.UTF8.GetString(bytes!).ShouldBe(HooksServiceRig.DefaultHooksJson);
        await rig.Service.SetHookEnabledAsync(guard.Hook.Id, enabled: true, ct).ConfigureAwait(true);
        Guard(await rig.OverviewAsync(ct).ConfigureAwait(true)).IsEnabled.ShouldBeTrue();
    }

    /// <summary>An unsafe session identifier is refused before any state is created.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("a/b")]
    public async Task AnUnsafeSessionIdIsRefused(string sessionId)
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);

        await Should.ThrowAsync<ArgumentException>(() => rig.Service.StartSessionAsync(rig.Request(sessionId), ct)).ConfigureAwait(true);
    }
}
