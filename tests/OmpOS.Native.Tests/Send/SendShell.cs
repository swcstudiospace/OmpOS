using Avalonia.Threading;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Native.Agents;
using OmpOS.Native.Engine;
using OmpOS.Native.Views.RunControls;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Runs;
using OmpOS.Presentation.State;

namespace OmpOS.Native.Tests.Send;

/// <summary>
/// The main window over the production head: an <see cref="AgentHead"/> whose engines talk to a scripted <c>omp acp</c> peer, with one
/// session created through New agent and shown. The session's send path, approval bridge and engine are the head's own, so the tests
/// drive the same send path the application runs.
/// </summary>
internal sealed class SendShell : IAsyncDisposable
{
    private readonly AgentHead _head;
    private readonly SessionSlot _slot;
    private readonly string _stateRoot;

    private SendShell(AgentHead head, SessionSlot slot, MainWindow window, string stateRoot)
    {
        _head = head;
        _slot = slot;
        Window = window;
        _stateRoot = stateRoot;
    }

    /// <summary>Gets the shown window.</summary>
    public MainWindow Window { get; }

    /// <summary>Gets the runtime of the session the window shows.</summary>
    public AgentSessionRuntime Runtime => _slot.Runtime ?? throw new InvalidOperationException("The session has no runtime.");

    /// <summary>Gets the approval bridge of the session: the head's own, which settles a decision in the session's engine once one exists.</summary>
    public IToolApprovalBridge Approvals => _slot.Approvals;

    /// <summary>Gets the transcript view model of the session.</summary>
    public SessionViewModel Session => Runtime.Session;

    /// <summary>Gets the composer view model of the session.</summary>
    public ComposerViewModel Composer => Runtime.Composer;

    /// <summary>Gets the run-input scheduler of the session.</summary>
    public RunInputScheduler Runs => Runtime.Runs;

    /// <summary>Gets the run controls of the session.</summary>
    public RunControlsViewModel RunControls => Runtime.RunControls;

    /// <summary>
    /// Builds a head whose engines use <paramref name="transportFactory"/>, creates one session through the panel's New agent action
    /// and shows the window over it. Showing the window starts no engine: the engine is composed on the first send.
    /// </summary>
    /// <param name="workspaceRoot">The workspace folder every session runs in. It must exist.</param>
    /// <param name="transportFactory">Creates the transport of each engine's ACP session.</param>
    /// <param name="allowedKinds">The tool kinds the approval policy admits. Empty admits none.</param>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The shell. Dispose it to close the window and stop the head's engines.</returns>
    public static async Task<SendShell> ShowAsync(
        string workspaceRoot,
        Func<AcpClientOptions, IAcpTransport> transportFactory,
        IReadOnlyCollection<string> allowedKinds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(transportFactory);
        ArgumentNullException.ThrowIfNull(allowedKinds);

        var stateRoot = Directory.CreateTempSubdirectory("ompos-send-state-").FullName;
        var head = HeadOf(new AgentHeadOptions
        {
            PluginsRoot = Path.Combine(stateRoot, "plugins"),
            StateRoot = Path.Combine(stateRoot, "state"),
            WorkspaceRoot = workspaceRoot,
            AllowedKinds = allowedKinds,
            TransportFactory = transportFactory,
            MaintenanceInterval = TimeSpan.Zero,
            Dispatch = RunOnUiThread,

            // These tests assert the engine's own queue: a steer or follow-up waits for the end of the turn and is sent as the next prompt.
            LiveSteering = false,
        });
        try
        {
            await head.InitializeAsync(cancellationToken).ConfigureAwait(true);
            await head.Panel.NewSessionAsync(cancellationToken).ConfigureAwait(true);

            var sessionId = head.Panel.Selected?.Id ?? throw new InvalidOperationException("New agent selected no session.");
            var slot = head.Host.SlotOf(sessionId) ?? throw new InvalidOperationException("The head opened no slot for the new session.");
            var window = new MainWindow(head.Panel, MotionPreference.System, workspaceRoot)
            {
                Width = 1280,
                Height = 800,
            };
            window.Show();
            ShellHarness.Settle(window);
            ShellHarness.Settle(window);
            var shell = new SendShell(head, slot, window, stateRoot);
            head = null;
            return shell;
        }
        finally
        {
            if (head is not null)
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }
    }

    private static AgentHead HeadOf(AgentHeadOptions options) => AgentHead.Create(options);

    /// <summary>Gets the engine of the session, composing it when no send has yet. Composing starts no process.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The session's composed engine.</returns>
    public Task<EngineComposition> EngineAsync(CancellationToken cancellationToken) => _slot.EngineAsync(cancellationToken);

    /// <summary>Puts <paramref name="text"/> in the composer's draft and sends it.</summary>
    /// <param name="text">The message.</param>
    /// <returns>A task that completes when the send was handed to the engine.</returns>
    public async Task SendAsync(string text)
    {
        Composer.Draft = text;
        await Composer.SendAsync().ConfigureAwait(true);
    }

    /// <summary>Adds one pending request to the session's run controls, as the engine's permission stream does, and settles the layout.</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>The request the window now shows.</returns>
    public PendingToolApproval AddRequest(string requestId)
    {
        var approval = new PendingToolApproval(_slot.Id, requestId, "bash", "command: ls");
        RunControls.Approvals.Add(approval);
        ShellHarness.Settle(Window);
        return approval;
    }

    /// <summary>Closes the window, stops the head's engines and removes the head's state.</summary>
    /// <returns>A task that completes when the head stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        Window.Close();
        await _head.DisposeAsync().ConfigureAwait(true);
        Directory.Delete(_stateRoot, recursive: true);
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
