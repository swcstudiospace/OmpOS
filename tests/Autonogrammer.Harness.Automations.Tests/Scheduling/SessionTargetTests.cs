using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Harness.Automations.Scheduling;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Automations.Tests.Scheduling;

/// <summary>Verifies session-target validation, starter routing and the Skip re-check at start.</summary>
public sealed class SessionTargetTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void The_API_refuses_a_session_target()
    {
        var issues = AutomationValidator.Validate(Session(), apiHost: true);

        issues.ShouldContain(issue => issue.Field == "target" && issue.Message.Contains("API", StringComparison.Ordinal));
    }

    [Fact]
    public void Skip_is_refused_at_save_without_filesystem_isolation()
    {
        var skipped = Session() with { Target = Session().Target with { ApprovalMode = SessionApprovalMode.Skip } };

        AutomationValidator.Validate(skipped).ShouldContain(issue => issue.Message.Contains("filesystem-isolated", StringComparison.Ordinal));
        AutomationValidator.Validate(skipped, isolation: new FixedIsolation(SessionIsolationLevel.FilesystemIsolated)).ShouldBeEmpty();
        AutomationValidator.Validate(skipped with { Target = skipped.Target with { SkipAcknowledged = true } }).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_conversation_surface_routes_to_its_starter_and_code_keeps_its_own()
    {
        var conversation = new RecordingStarter(ScheduledSessionSurface.Cowork);
        var code = new RecordingStarter(ScheduledSessionSurface.Code);
        var store = new InMemoryAutomationStore();
        var runner = new AutomationRunner(store, [], new FakeTimeProvider(Kit.Start), [conversation, code], new FixedIsolation(SessionIsolationLevel.FilesystemIsolated));
        await using var runnerScope = runner.ConfigureAwait(true);
        await store.SaveAsync(Session(ScheduledSessionSurface.Cowork, SessionApprovalMode.Skip), Token).ConfigureAwait(true);
        await store.SaveAsync(Session(ScheduledSessionSurface.Code, SessionApprovalMode.Skip, "aut_code"), Token).ConfigureAwait(true);

        var conversationRun = await runner.StartAsync(Session(ScheduledSessionSurface.Cowork, SessionApprovalMode.Skip), AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var codeRun = await runner.StartAsync(Session(ScheduledSessionSurface.Code, SessionApprovalMode.Skip, "aut_code"), AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var conversationEntry = await conversationRun.Completion!.WaitAsync(Token).ConfigureAwait(true);
        _ = await codeRun.Completion!.WaitAsync(Token).ConfigureAwait(true);

        conversation.Calls.ShouldHaveSingleItem().Surface.ShouldBe(ScheduledSessionSurface.Cowork);
        code.Calls.ShouldHaveSingleItem().Surface.ShouldBe(ScheduledSessionSurface.Code);
        conversationEntry.Source.ShouldBe(AutomationRunSource.Manual);
        conversationEntry.Summary.ShouldContain("schedule.run");
        conversationEntry.Status.ShouldBe(AutomationRunStatus.Launched);
    }

    [Fact]
    public async Task Skip_is_refused_again_at_start_without_filesystem_isolation()
    {
        var starter = new RecordingStarter(ScheduledSessionSurface.Cowork);
        var store = new InMemoryAutomationStore();
        var runner = new AutomationRunner(store, [], new FakeTimeProvider(Kit.Start), [starter], new FixedIsolation(SessionIsolationLevel.NotIsolated));
        await using var runnerScope = runner.ConfigureAwait(true);
        var automation = Session(approval: SessionApprovalMode.Skip) with
        {
            Target = Session().Target with { ApprovalMode = SessionApprovalMode.Skip, SkipAcknowledged = true },
        };
        await store.SaveAsync(automation, Token).ConfigureAwait(true);

        var start = await runner.StartAsync(automation, AutomationTriggerContext.Cron(Kit.Start), Token).ConfigureAwait(true);
        var entry = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);

        starter.Calls.ShouldBeEmpty();
        entry.Status.ShouldBe(AutomationRunStatus.Skipped);
        entry.Error.ShouldNotBeNull().ShouldContain("SkipRefusedNoIsolation");
        entry.Source.ShouldBe(AutomationRunSource.Cron);
        entry.Summary.ShouldContain(Kit.Start.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static UserAutomation Session(
        ScheduledSessionSurface surface = ScheduledSessionSurface.Cowork,
        SessionApprovalMode approval = SessionApprovalMode.Manual,
        string id = "aut_session") =>
        TestAutomations.Make(id: id) with
        {
            Target = new AutomationTargetSpec
            {
                Kind = AutomationTargetKind.Session,
                Surface = surface,
                ProjectId = "proj_1",
                TrustedFolders = ["/work"],
                ApprovalMode = approval,
                Model = "test-model",
                Preset = SchedulePreset.Daily,
                MissedRunPolicy = MissedRunPolicy.RunOnce,
            },
        };

    private sealed class FixedIsolation(SessionIsolationLevel level) : ISessionIsolationLevel
    {
        public SessionIsolationLevel Level => level;
    }

    private sealed class RecordingStarter(ScheduledSessionSurface surface) : IScheduledSessionStarter
    {
        private readonly List<Call> _calls = [];

        public ScheduledSessionSurface Surface { get; } = surface;

        public IReadOnlyList<Call> Calls => _calls;

        public Task<ScheduledSessionStart> StartAsync(
            ScheduledSessionSurface surface,
            string? projectId,
            IReadOnlyList<string> trustedFolders,
            SessionApprovalMode approvalMode,
            string? model,
            string prompt,
            CancellationToken cancellationToken)
        {
            _calls.Add(new Call(surface, projectId, prompt));
            return Task.FromResult(new ScheduledSessionStart(true, "sess-" + surface, null));
        }
    }

    private sealed record Call(ScheduledSessionSurface Surface, string? ProjectId, string Prompt);
}
