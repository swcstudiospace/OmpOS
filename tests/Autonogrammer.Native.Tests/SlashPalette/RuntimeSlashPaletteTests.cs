using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Tests.Terminal;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Commands;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.SlashPalette;

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
            palette.Entries.ShouldNotContain(static entry => entry.Source == SlashPaletteSource.Engine, "omp has advertised nothing yet");
            palette.Entries.Count.ShouldBe(SlashCommandCatalogue.Shipped.ListAll().Count, "every catalogue row is listed, each with its status");
            palette.Entries.Single(static entry => string.Equals(entry.Name, "plan", StringComparison.Ordinal)).IsEnabled.ShouldBeTrue("the composer itself supplies the mode action");
            palette.Entries.Single(static entry => string.Equals(entry.Name, "clear", StringComparison.Ordinal)).IsEnabled.ShouldBeFalse("no shell has supplied the new-chat action yet");
            palette.Entries.Single(static entry => string.Equals(entry.Name, "goal", StringComparison.Ordinal)).DisabledReason.ShouldNotBeNullOrWhiteSpace("an unavailable token says why");

            control.Report(FakeSessionCommands.Typical);

            palette.Entries.Where(static entry => entry.Source == SlashPaletteSource.Engine).Select(static entry => entry.Name).ShouldBe(["fast", "model", "security", "skill:review"]);
            palette.Entries.ShouldNotContain(static entry => string.Equals(entry.Name, "fast", StringComparison.Ordinal) && entry.Source == SlashPaletteSource.Autonogrammer, "omp runs /fast itself, so its row stands for the catalogue's");
            palette.Entries.ShouldNotContain(static entry => string.Equals(entry.Name, "review", StringComparison.Ordinal), "omp's skill:review stands for the Cursor built-in skill row");
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

            // The palette always lists the shipped catalogue; what must stop is the engine's own commands appearing after the dispose.
            var names = palette.Entries.Select(static entry => entry.Name).ToList();
            names.ShouldNotContain("security", StringComparer.Ordinal);
            names.ShouldNotContain("skill:review", StringComparer.Ordinal);
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
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "autonogrammer-shell-tests", sessionId),
            },
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            SessionCommands = control,
            ConnectModels = connect,
            TerminalStarter = kind == SessionKind.Terminal ? new FakeOmpTerminalStarter() : null,
        });
    }
}
