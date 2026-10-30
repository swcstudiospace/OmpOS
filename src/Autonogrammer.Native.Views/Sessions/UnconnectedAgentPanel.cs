using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Approvals;

namespace Autonogrammer.Native.Views.Sessions;

/// <summary>
/// Builds an Agent Panel for a head that has no engine, for example the Android head and the XAML designer. The panel holds one selected
/// session whose send path is an <see cref="UnconnectedPromptChannel"/>: every send is refused with <see cref="MainView.RefusedSendStatus"/>,
/// no process is started, and New agent reports that no engine is connected. Nothing here needs releasing: there is no engine, and the
/// stage source ends as soon as it is observed.
/// </summary>
public static class UnconnectedAgentPanel
{
    /// <summary>The identifier of the one session of the panel.</summary>
    public const string SessionId = "unconnected";

    /// <summary>The title of the one session of the panel.</summary>
    public const string SessionTitle = "Agent";

    // The host of a panel lives exactly as long as the panel does: nothing else references it.
    private static readonly ConditionalWeakTable<AgentPanelViewModel, UnconnectedHost> Owners = new();

    /// <summary>Creates the panel with its one session selected.</summary>
    /// <param name="approvals">The bridge that records the session's tool-approval decisions.</param>
    /// <param name="workspaceRoot">The workspace folder the session is scoped to, or null for the current folder.</param>
    /// <param name="motion">The motion setting of the head; the session's runtime keeps it.</param>
    /// <returns>The panel view model. Pass it to <see cref="MainView"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="approvals"/> is null.</exception>
    public static AgentPanelViewModel Create(IToolApprovalBridge approvals, string? workspaceRoot, MotionPreference motion)
    {
        ArgumentNullException.ThrowIfNull(approvals);

        var now = TimeProvider.System.GetUtcNow();
        var catalog = new SingleSessionCatalog(new SessionSummary
        {
            Id = SessionId,
            Title = SessionTitle,
            Lifecycle = SessionLifecycle.Created,
            CreatedAt = now,
            LastActivity = now,
            WorkspaceRoot = workspaceRoot,
        });
        var host = HostOf(catalog, approvals, workspaceRoot ?? Environment.CurrentDirectory, motion);
        var panel = new AgentPanelViewModel(host.Model, host);
        Owners.Add(panel, host);
        panel.SelectSession(SessionId);
        return panel;
    }

    private static UnconnectedHost HostOf(ISessionCatalog catalog, IToolApprovalBridge approvals, string workspaceRoot, MotionPreference motion) =>
        new(catalog, approvals, workspaceRoot, motion);

    /// <summary>The registry of the panel: it lists its one session and never changes.</summary>
    private sealed class SingleSessionCatalog(SessionSummary session) : ISessionCatalog
    {
        /// <inheritdoc />
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public IReadOnlyList<SessionSummary> Snapshot() => [session];
    }

    /// <summary>
    /// The head of the panel: it opens the one session over the unconnected send path and starts nothing. It keeps the rail model and the
    /// runtime for as long as the panel lives; neither holds a timer, a process or a file.
    /// </summary>
    private sealed class UnconnectedHost(ISessionCatalog catalog, IToolApprovalBridge approvals, string workspaceRoot, MotionPreference motion) : IAgentPanelHost, IAsyncDisposable
    {
        private AgentSessionRuntime? _runtime;

        /// <summary>Gets the rail model over the one session.</summary>
        public SessionCatalogModel Model { get; } = new(catalog, TimeProvider.System);

        /// <inheritdoc />
        public Task<string> CreateSessionAsync(CancellationToken cancellationToken) =>
            Task.FromException<string>(new InvalidOperationException(UnconnectedPromptChannel.UnavailableText));

        /// <inheritdoc />
        public Task<string> CreateTerminalSessionAsync(CancellationToken cancellationToken) =>
            Task.FromException<string>(new InvalidOperationException(UnconnectedPromptChannel.UnavailableText));

        /// <inheritdoc />
        public AgentSessionViewModel OpenSession(AgentRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            _runtime = new AgentSessionRuntime(new AgentSessionRuntimeOptions
            {
                Row = row,
                Prompts = new UnconnectedPromptChannel(),
                Approvals = approvals,
                Scope = new StageObservationScope
                {
                    SessionId = row.Id,
                    WorkspaceRoot = workspaceRoot,
                    SessionStateDirectory = Path.Combine(Path.GetTempPath(), "autonogrammer-unconnected", row.Id),
                },
                Stages = new NoStageSource(),
                Context = new NoTaskContextSource(),
                Motion = motion,
            });
            _runtime.StartObserving();
            return _runtime.ViewModel;
        }

        /// <inheritdoc />
        public Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task ReloadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>Releases the runtime and the rail model.</summary>
        /// <returns>A task that completes when both are released.</returns>
        public async ValueTask DisposeAsync()
        {
            Model.Dispose();
            if (_runtime is not null)
            {
                await _runtime.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>A stage source of a session with no plugins: it ends at once without an event.</summary>
    private sealed class NoStageSource : IStageSource
    {
        /// <inheritdoc />
        public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }
    }

    /// <summary>A task-context source of a session that is bound to no task: it answers a context with no content.</summary>
    private sealed class NoTaskContextSource : ITaskContextSource
    {
        /// <inheritdoc />
        public Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(new TaskContext
            {
                Title = SessionTitle,
                SharedMemory = [],
                Lessons = [],
                Warnings = [],
            });
    }
}
