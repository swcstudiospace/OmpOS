using OmpOS.Contracts.Agents;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Tests.Terminal;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.OptionChips;

/// <summary>The rules by which a runtime creates the mode and thinking chips of its composer: only a chat, only for options omp reports, disabled while a response runs.</summary>
public sealed class RuntimeOptionChipTests
{
    /// <summary>A chat runtime has no chips until omp reports the options; each chip appears for its own option, and a missing option leaves its chip null.</summary>
    [Fact]
    public async Task AChatRuntimeShowsAChipForEachOptionOmpReports()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var control = new FakeSessionOptions();
            var runtime = Create(control, SessionKind.Chat);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            runtime.Composer.ModeChip.ShouldBeNull();
            runtime.Composer.ThinkingChip.ShouldBeNull();

            control.Report(FakeSessionOptions.Mode());

            runtime.Composer.ModeChip.ShouldNotBeNull().OptionId.ShouldBe("mode");
            runtime.Composer.ThinkingChip.ShouldBeNull("omp reported no thinking option");

            control.Report(FakeSessionOptions.Mode(), FakeSessionOptions.Thinking());

            runtime.Composer.ThinkingChip.ShouldNotBeNull().OptionId.ShouldBe("thinking");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The plan mode's chip tooltip is omp's own plain-language description of planning.</summary>
    [Fact]
    public async Task ThePlanModeChipExplainsPlanningInItsTooltip()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var control = new FakeSessionOptions();
            control.Report(FakeSessionOptions.Mode("plan"));
            var runtime = Create(control, SessionKind.Chat);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            var chip = runtime.Composer.ModeChip.ShouldNotBeNull();

            chip.Label.ShouldBe("Plan");
            chip.CurrentDescription.ShouldBe(FakeSessionOptions.PlanDescription);
            chip.ToolTipText.ShouldContain("drafts a plan to a markdown file before any code changes", Case.Sensitive);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The chips follow the composer's streaming state: a running response disables them, and the end of the response enables them again.</summary>
    [Fact]
    public async Task AStreamingResponseDisablesTheChips()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var control = new FakeSessionOptions();
            control.Report(FakeSessionOptions.Mode(), FakeSessionOptions.Thinking());
            var runtime = Create(control, SessionKind.Chat);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var mode = runtime.Composer.ModeChip.ShouldNotBeNull();
            var thinking = runtime.Composer.ThinkingChip.ShouldNotBeNull();
            mode.IsEnabled.ShouldBeTrue();

            runtime.Composer.IsStreaming = true;

            mode.IsEnabled.ShouldBeFalse();
            thinking.IsEnabled.ShouldBeFalse();
            mode.ToolTipText.ShouldBe("This setting cannot be changed while a response is running.", Case.Sensitive);

            runtime.Composer.IsStreaming = false;

            mode.IsEnabled.ShouldBeTrue();
            thinking.IsEnabled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal runs its own omp, which owns its settings: no chips, even when options are composed and reported.</summary>
    [Fact]
    public async Task ATerminalRuntimeHasNoChips()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var control = new FakeSessionOptions();
            var runtime = Create(control, SessionKind.Terminal);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            control.Report(FakeSessionOptions.Mode(), FakeSessionOptions.Thinking());

            runtime.Composer.ModeChip.ShouldBeNull();
            runtime.Composer.ThinkingChip.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A runtime with no session options composed has no chips.</summary>
    [Fact]
    public async Task ARuntimeWithoutOptionsHasNoChips()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(null, SessionKind.Chat);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            runtime.Composer.ModeChip.ShouldBeNull();
            runtime.Composer.ThinkingChip.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Disposing the runtime stops the chips from following omp.</summary>
    [Fact]
    public async Task ADisposedRuntimeStopsFollowingOmp()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var control = new FakeSessionOptions();
            var runtime = Create(control, SessionKind.Chat);
            await runtime.DisposeAsync().ConfigureAwait(true);

            control.Report(FakeSessionOptions.Mode());

            runtime.Composer.ModeChip.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static AgentSessionRuntime Create(FakeSessionOptions? control, SessionKind kind)
    {
        var sessionId = kind == SessionKind.Terminal ? "session-chips-terminal" : "session-chips";
        var now = DateTimeOffset.UnixEpoch;
        var catalog = new FakeAgentCatalog();
        catalog.Put(new SessionSummary
        {
            Id = sessionId,
            Title = $"Agent {sessionId}",
            Lifecycle = SessionLifecycle.Created,
            CreatedAt = now,
            LastActivity = now,
            Kind = kind,
        });
        using var model = new SessionCatalogModel(catalog, new ShellClock());
        model.Refresh();
        return new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = model.Sessions.Single(),
            Prompts = kind == SessionKind.Terminal ? new UnconnectedPromptChannel() : new GatedPromptChannel(),
            Approvals = new UnconnectedToolApprovalBridge(),
            Scope = new StageObservationScope
            {
                SessionId = sessionId,
                WorkspaceRoot = Environment.CurrentDirectory,
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "ompos-shell-tests", sessionId),
            },
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            SessionOptions = control,
            TerminalStarter = kind == SessionKind.Terminal ? new FakeOmpTerminalStarter() : null,
        });
    }
}
