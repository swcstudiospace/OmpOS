using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Terminal;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Terminal;

/// <summary>
/// A persisted terminal record in the Native head (Phase 81): the runtime is retired, so it never calls the starter, holds no terminal
/// view model, and reports the persisted kind for listing. The fake starter is the never-called witness: omp is never launched.
/// </summary>
public sealed class RetiredTerminalRuntimeTests
{
    /// <summary>A runtime built for a persisted Terminal record never calls the fake starter, reports retired, and disposes without starting anything.</summary>
    [Fact]
    public async Task APersistedTerminalRuntimeNeverStartsOmp()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var starter = new FakeOmpTerminalStarter();
            var runtime = Create(starter);
            await using var scope = runtime.ConfigureAwait(true);

            runtime.IsRetiredTerminal.ShouldBeTrue();
            runtime.Kind.ShouldBe(SessionKind.Terminal, "the persisted kind is kept for listing");
            runtime.Terminal.ShouldBeNull("a retired record has no terminal view model");
            starter.Sizes.ShouldBeEmpty("omp is never launched for a retired record");
            starter.Sessions.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A chat runtime is unaffected: no starter means no retired flag and a chat kind.</summary>
    [Fact]
    public async Task AChatRuntimeIsNotRetired()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(starter: null, sessionId: "session-2");
            await using var scope = runtime.ConfigureAwait(true);

            runtime.IsRetiredTerminal.ShouldBeFalse();
            runtime.Kind.ShouldBe(SessionKind.Chat);
            runtime.Terminal.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static AgentSessionRuntime Create(IOmpTerminalStarter? starter, string sessionId = "session-1")
    {
        var now = DateTimeOffset.UnixEpoch;
        var catalog = new FakeAgentCatalog();
        catalog.Put(new SessionSummary
        {
            Id = sessionId,
            Title = $"Agent {sessionId}",
            Lifecycle = SessionLifecycle.Created,
            CreatedAt = now,
            LastActivity = now,
            Kind = starter is null ? SessionKind.Chat : SessionKind.Terminal,
        });
        using var model = new SessionCatalogModel(catalog, new ShellClock());
        model.Refresh();
        var row = model.Sessions.Single();
        return new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = row,
            Prompts = new UnconnectedPromptChannel(),
            Approvals = new UnconnectedToolApprovalBridge(),
            Scope = new StageObservationScope
            {
                SessionId = sessionId,
                WorkspaceRoot = Environment.CurrentDirectory,
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "autonogrammer-shell-tests", sessionId),
            },
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            TerminalStarter = starter,
        });
    }
}
