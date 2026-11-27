using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Terminal;

/// <summary>
/// A persisted terminal record in the Native head (Phase 81): the runtime is retired. No starter input exists any more, so nothing can start
/// omp for the record: it reports retired and keeps the persisted kind for listing.
/// </summary>
public sealed class RetiredTerminalRuntimeTests
{
    /// <summary>A runtime built for a persisted Terminal record is retired and keeps the persisted kind.</summary>
    [Fact]
    public async Task APersistedTerminalRuntimeIsRetired()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(SessionKind.Terminal);
            await using var scope = runtime.ConfigureAwait(true);

            runtime.IsRetiredTerminal.ShouldBeTrue();
            runtime.Kind.ShouldBe(SessionKind.Terminal, "the persisted kind is kept for listing");
            runtime.Runs.IsRunActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A chat runtime is unaffected: no retired flag and a chat kind.</summary>
    [Fact]
    public async Task AChatRuntimeIsNotRetired()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(SessionKind.Chat, "session-2");
            await using var scope = runtime.ConfigureAwait(true);

            runtime.IsRetiredTerminal.ShouldBeFalse();
            runtime.Kind.ShouldBe(SessionKind.Chat);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static AgentSessionRuntime Create(SessionKind kind, string sessionId = "session-1")
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
            Kind = kind,
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
        });
    }
}
