using OmpOS.Contracts.Agents;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Tests.Terminal;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Commands;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.SlashPalette;

/// <summary>The rules by which a runtime gives its composer a slash palette: only a chat, only when the session offers commands, listing what omp advertises, and following omp until the runtime is disposed.</summary>
public sealed class RuntimeSlashPaletteTests
{
    /// <summary>A chat runtime has a palette from the start; it lists omp's commands once omp advertises them, and the composer opens it for a draft that starts with a slash.</summary>
    [Fact]
    public async Task AChatRuntimeListsTheCommandsOmpAdvertises()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var control = new FakeSessionCommands();
            var runtime = Create(control, SessionKind.Chat);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var palette = runtime.Composer.SlashPalette.ShouldNotBeNull();

            runtime.Composer.Draft = "/";
            palette.IsOpen.ShouldBeTrue();
            palette.Entries.ShouldBeEmpty("no OmpOS entry is listed: the shipped catalogue has no handler, so only what omp advertises is offered");
            palette.Entries.ShouldNotContain(static entry => entry.Name == "security");

            control.Report(FakeSessionCommands.Typical);

            palette.Entries.Select(static entry => entry.Name).ShouldBe(["fast", "model", "security", "skill:review"]);
            palette.Entries.ShouldAllBe(static entry => entry.Source == SlashPaletteSource.Omp);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal runs its own omp, which shows its own commands: no palette, even when commands are composed and reported.</summary>
    [Fact]
    public async Task ATerminalRuntimeHasNoPalette()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var control = new FakeSessionCommands();
            var runtime = Create(control, SessionKind.Terminal);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            control.Report(FakeSessionCommands.Typical);

            runtime.Composer.SlashPalette.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A runtime whose session offers no commands has no palette.</summary>
    [Fact]
    public async Task ARuntimeWithoutCommandsHasNoPalette()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(null, SessionKind.Chat);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            runtime.Composer.SlashPalette.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opening the palette starts the engine session through the runtime's connect callback, so a session that has not sent a prompt still lists omp's commands.</summary>
    [Fact]
    public async Task OpeningThePaletteStartsTheEngineSession()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var control = new FakeSessionCommands();
            var connects = 0;
            var runtime = Create(
                control,
                SessionKind.Chat,
                _ =>
                {
                    connects++;
                    control.Report(FakeSessionCommands.Typical);
                    return Task.CompletedTask;
                });
            await using var runtimeScope = runtime.ConfigureAwait(true);

            runtime.Composer.Draft = "/sec";

            connects.ShouldBe(1);
            runtime.Composer.SlashPalette.ShouldNotBeNull().Entries.ShouldHaveSingleItem().Name.ShouldBe("security");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Disposing the runtime stops the palette from following omp.</summary>
    [Fact]
    public async Task ADisposedRuntimeStopsFollowingOmp()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var control = new FakeSessionCommands();
            var runtime = Create(control, SessionKind.Chat);
            var palette = runtime.Composer.SlashPalette.ShouldNotBeNull();
            await runtime.DisposeAsync().ConfigureAwait(true);

            control.Report(FakeSessionCommands.Typical);
            runtime.Composer.Draft = "/";

            palette.Entries.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static AgentSessionRuntime Create(FakeSessionCommands? control, SessionKind kind, Func<CancellationToken, Task>? connect = null)
    {
        var sessionId = kind == SessionKind.Terminal ? "session-slash-terminal" : "session-slash";
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
            SessionCommands = control,
            ConnectModels = connect,
            TerminalStarter = kind == SessionKind.Terminal ? new FakeOmpTerminalStarter() : null,
        });
    }
}
