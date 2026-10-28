using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.Plugins;
using Autonogrammer.Harness.Plugins.Hooks;
using Autonogrammer.Native.Views.Hooks;
using Autonogrammer.Presentation.Hooks;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;
using HarnessNotice = Autonogrammer.Harness.AgentLoop.Hooks.HookNotice;
using HarnessNoticeKind = Autonogrammer.Harness.AgentLoop.Hooks.HookNoticeKind;

namespace Autonogrammer.Native.Tests.Hooks;

/// <summary>The adapter between the hooks service and the Hooks page: what it maps, what it passes through, and how it reports failures.</summary>
public sealed class HooksServiceSourceTests
{
    private const string Root = "/work";
    private static readonly DateTimeOffset Noon = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static EffectiveHook Hook(
        string id,
        string eventName = "beforeShellExecution",
        HookEvent? hookEvent = HookEvent.BeforeShellExecution,
        bool isOmp = false,
        HookScope scope = HookScope.Project,
        bool preConsented = false,
        string? description = null,
        IReadOnlyDictionary<string, string>? env = null) =>
        new(
            id,
            "cursor-project:abc",
            scope,
            "/work/.cursor/hooks.json",
            eventName,
            hookEvent,
            isOmp,
            2,
            HookType.Command,
            "./guard.sh",
            null,
            HookShell.PosixSh,
            null,
            null,
            TimeSpan.FromSeconds(12),
            null,
            true,
            "git push",
            true,
            description,
            env ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["TOKEN_FILE"] = "/secret/path", ["NO_COLOR"] = "1" },
            HookLocations.Both,
            "/work",
            "/work",
            id + "-hash",
            HookConsentIdentity.CurrentVersion,
            preConsented,
            true,
            null,
            null);

    private static HooksOverview Overview(params HookInfo[] hooks) => new()
    {
        RunHooks = true,
        WorkspaceRoots = [Root],
        Set = new EffectiveHookSet([.. hooks.Select(static h => h.Hook)], [], [new HookDiagnostic(HookDiagnosticSeverity.Error, "/work/bad.json", "$.version", "version must be 1"), new HookDiagnostic(HookDiagnosticSeverity.Warning, "/work/.cursor/hooks.json", "$.x", "unknown key")]),
        Hooks = hooks,
        WithheldSources = [new HookSource(HookScope.Project, "/work/.autonogrammer/hooks.json", HookConfigFormat.HooksJson, "/work", "/work", "autonogrammer-project:x", 1)],
    };

    private static HookInfo Info(EffectiveHook hook, bool enabled = true, bool consented = true) => new() { Hook = hook, IsEnabled = enabled, IsConsented = consented };

    private static (HooksServiceSource Source, FakeHooksService Service) Create(HooksOverview overview)
    {
        var service = new FakeHooksService { Overview = overview };
        return (new HooksServiceSource(service, static () => new HooksWorkspace { Roots = [Root, "/work/second"] }), service);
    }

    private static HookRunRecord Record(string hookId, HookRunStatus status = HookRunStatus.Failed) => new(
        Noon,
        "s1",
        "g1",
        "beforeShellExecution",
        hookId,
        "cursor-project:abc",
        HookScope.User,
        "Shell",
        status,
        "none",
        1,
        TimeSpan.FromMilliseconds(80),
        "message",
        "out",
        "err",
        true,
        "00",
        "note");

    /// <summary>The overview becomes a snapshot: scope, event, command, timeout, failure policy, state flags and the environment names only, never their values.</summary>
    [Fact]
    public async Task TheOverviewBecomesASnapshot()
    {
        var quarantined = Info(Hook("q", scope: HookScope.ClaudeUser), enabled: false, consented: false) with { Quarantine = new PluginQuarantine("cursor-project:abc", "the shell failed", Noon) };
        var (source, _) = Create(Overview(Info(Hook("guard", description: "No force pushes")), quarantined));

        var snapshot = await source.LoadAsync(Token).ConfigureAwait(true);

        snapshot.RunHooks.ShouldBeTrue();
        snapshot.WorkspaceRoot.ShouldBe(Root);
        var guard = snapshot.Hooks[0];
        guard.Id.ShouldBe("guard");
        guard.DisplayName.ShouldBe("No force pushes");
        guard.Scope.ShouldBe(HookScopeKind.Project);
        guard.EventName.ShouldBe("beforeShellExecution");
        guard.Command.ShouldBe("./guard.sh");
        guard.ShellName.ShouldBe("PosixSh");
        guard.TimeoutSeconds.ShouldBe(12);
        guard.FailClosed.ShouldBeTrue();
        guard.IsGuarding.ShouldBeTrue();
        guard.IsConsented.ShouldBeTrue();
        guard.EnvKeys.ShouldBe(["NO_COLOR", "TOKEN_FILE"]);
        guard.WorkingDirectory.ShouldBe(".");
        var other = snapshot.Hooks[1];
        other.DisplayName.ShouldBe("beforeShellExecution #3");
        other.Scope.ShouldBe(HookScopeKind.ClaudeUser);
        other.IsEnabled.ShouldBeFalse();
        other.QuarantinedSource.ShouldBe("cursor-project:abc");
        other.QuarantineReason.ShouldBe("the shell failed");
        snapshot.Diagnostics.Select(static d => d.IsError).ToArray().ShouldBe([true, false]);
        snapshot.Diagnostics[0].File.ShouldBe("/work/bad.json");
        snapshot.WithheldFiles.ShouldBe(["/work/.autonogrammer/hooks.json"]);
    }

    /// <summary>A Tab event is not applicable, an event name Autonogrammer does not know is unknown, and an omp-section hook is known.</summary>
    [Fact]
    public async Task EventsAutonogrammerCannotProduceOrDoesNotKnowAreFlagged()
    {
        var (source, _) = Create(Overview(
            Info(Hook("tab", "beforeTabFileRead", HookEvent.BeforeTabFileRead)),
            Info(Hook("mystery", "madeUpEvent", null)),
            Info(Hook("omp", "tool_call", null, isOmp: true))));

        var hooks = (await source.LoadAsync(Token).ConfigureAwait(true)).Hooks;

        hooks[0].IsApplicable.ShouldBeFalse();
        hooks[0].IsKnownEvent.ShouldBeTrue();
        hooks[1].IsKnownEvent.ShouldBeFalse();
        hooks[2].IsKnownEvent.ShouldBeTrue();
        hooks[2].IsOmpEvent.ShouldBeTrue();
        hooks[2].IsApplicable.ShouldBeTrue();
    }

    /// <summary>The last run and the last approved version are mapped; a shell of none becomes no shell.</summary>
    [Fact]
    public async Task RunsAndApprovedVersionsAreMapped()
    {
        var approved = new HookApprovalSnapshot
        {
            HookId = "guard",
            EventName = "beforeShellExecution",
            Scope = "Project",
            SourceFile = "/work/.cursor/hooks.json",
            Args = ["prettier", "--write"],
            Shell = "None",
            TimeoutSeconds = 30,
            EnvKeys = ["A"],
            WorkingDirectory = ".",
            CommandHash = "h",
            ApprovedAt = Noon,
        };
        var (source, _) = Create(Overview(Info(Hook("guard")) with { LastRun = Record("guard", HookRunStatus.TimedOut), LastApproved = approved }));

        var hook = (await source.LoadAsync(Token).ConfigureAwait(true)).Hooks.Single();

        var run = hook.LastRun.ShouldNotBeNull();
        run.Outcome.ShouldBe(HookRunOutcome.TimedOut);
        run.Scope.ShouldBe(HookScopeKind.User);
        run.DurationMs.ShouldBe(80);
        run.HookName.ShouldBe("beforeShellExecution #3");
        run.OutputTruncated.ShouldBeTrue();
        var version = hook.LastApproved.ShouldNotBeNull();
        version.ShellName.ShouldBeNull();
        version.Arguments.ShouldBe(["prettier", "--write"]);
        version.TimeoutSeconds.ShouldBe(30);
        version.ApprovedAt.ShouldBe(Noon);
    }

    /// <summary>Actions pass through to the service for the hook of the last load; Trust all approves only the hooks without consent; Trust folder trusts every root.</summary>
    [Fact]
    public async Task ActionsReachTheServiceForTheRightHooks()
    {
        var (source, service) = Create(Overview(Info(Hook("a"), consented: false), Info(Hook("b")), Info(Hook("c"), consented: false)));
        await source.LoadAsync(Token).ConfigureAwait(true);

        await source.ApproveAsync("b", Token).ConfigureAwait(true);
        await source.RevokeAsync("a", Token).ConfigureAwait(true);
        await source.ApproveAllAsync(Token).ConfigureAwait(true);
        await source.SetEnabledAsync("c", false, Token).ConfigureAwait(true);
        await source.SetRunHooksAsync(false, Token).ConfigureAwait(true);
        await source.TrustWorkspaceAsync(Token).ConfigureAwait(true);
        await source.LiftQuarantineAsync("cursor-project:abc", "reinstalled", Token).ConfigureAwait(true);

        service.Calls.ShouldBe(
        [
            "LoadOverview",
            "Approve(b)",
            "Revoke(a)",
            "ApproveAll(a,c)",
            "SetHookEnabled(c, False)",
            "SetRunHooks(False)",
            "SetWorkspaceTrusted(/work, True)",
            "SetWorkspaceTrusted(/work/second, True)",
            "LiftQuarantine(cursor-project:abc, reinstalled)",
        ]);
    }

    /// <summary>A hook that is not in the last load, a quarantine that is already gone and a file system failure each become the page's inline error.</summary>
    [Fact]
    public async Task FailuresBecomeInlineErrors()
    {
        var (source, service) = Create(Overview(Info(Hook("a"))));
        await source.LoadAsync(Token).ConfigureAwait(true);

        await Should.ThrowAsync<SettingsSourceException>(() => source.ApproveAsync("gone", Token)).ConfigureAwait(true);
        service.ChangeFailure = new IOException("disk full");
        var failure = await Should.ThrowAsync<SettingsSourceException>(() => source.SetEnabledAsync("a", true, Token)).ConfigureAwait(true);
        failure.InnerException.ShouldBeOfType<IOException>();
        service.LoadFailure = new UnauthorizedAccessException("denied");
        await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);
    }

    /// <summary>Records the service raises reach subscribers mapped, with the hook's name from the last load, and stop reaching them after the last unsubscribe.</summary>
    [Fact]
    public async Task RunsAreForwardedUntilTheLastSubscriberLeaves()
    {
        var (source, service) = Create(Overview(Info(Hook("guard", description: "No force pushes"))));
        await source.LoadAsync(Token).ConfigureAwait(true);
        var seen = new List<HookRunItem>();
        var running = 0;
        void OnRun(object? sender, HookRunItemEventArgs e) => seen.Add(e.Run);
        void OnRunning(object? sender, EventArgs e) => running++;
        source.RunRecorded += OnRun;
        source.RunningChanged += OnRunning;

        service.Publish(Record("guard"));
        service.SetRunning("guard");

        var run = seen.ShouldHaveSingleItem();
        run.HookName.ShouldBe("No force pushes");
        run.Outcome.ShouldBe(HookRunOutcome.Failed);
        running.ShouldBe(1);
        source.RunningHookIds.ShouldBe(["guard"]);

        source.RunRecorded -= OnRun;
        source.RunningChanged -= OnRunning;
        service.Publish(Record("guard"));
        service.SetRunning();

        seen.Count.ShouldBe(1);
        running.ShouldBe(1);
    }

    /// <summary>A service notice becomes the thread's notice entry with its kind, tool call and counts.</summary>
    [Fact]
    public void NoticesMapToTheThreadsEntries()
    {
        var denied = HooksServiceSource.MapNotice(new HarnessNotice { Kind = HarnessNoticeKind.Denied, SessionId = "s", At = Noon, EventName = "beforeShellExecution", ToolCallId = "c1", Message = "no" }, "guard");
        var followUp = HooksServiceSource.MapNotice(new HarnessNotice { Kind = HarnessNoticeKind.FollowUp, SessionId = "s", At = Noon, EventName = "stop", Message = "x", Count = 2, Limit = 5 }, "stop");

        denied.ShouldBe(new HookNoticeEntry(HookNoticeType.Denied, "guard", "c1", "no"));
        followUp.ShouldBe(new HookNoticeEntry(HookNoticeType.FollowUp, "stop", null, "x", 2, 5));
        HooksServiceSource.MapNotice(new HarnessNotice { Kind = HarnessNoticeKind.Context, SessionId = "s", At = Noon, EventName = "sessionStart", Message = "ctx" }, string.Empty).Type.ShouldBe(HookNoticeType.Context);
        HooksServiceSource.MapNotice(new HarnessNotice { Kind = HarnessNoticeKind.ConsentRequired, SessionId = "s", At = Noon, EventName = "x", Message = "m" }, "g").Type.ShouldBe(HookNoticeType.ConsentRequired);
    }
}
