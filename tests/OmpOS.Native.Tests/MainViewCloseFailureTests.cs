using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Mcp;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Tests.Parity;
using OmpOS.Native.Views.Mcp;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests;

/// <summary>
/// Native checks for closing the shell when an engine host or the MCP host fails to dispose. The runtime of a session owns its engine
/// and disposes it once; a failure surfaces to the head that disposes the runtime, which traces it. The main view owns only the MCP host:
/// the window is already closing, so a failure cannot reach the status line and the view's continuation traces it instead.
/// </summary>
public sealed class MainViewCloseFailureTests
{
    /// <summary>How long the test waits for the close failure to reach the trace or for the stage observation to start.</summary>
    private static readonly TimeSpan TraceTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The fragment the MCP host's close continuation traces when its dispose fails.</summary>
    private const string McpMarker = "could not close the MCP tools";

    /// <summary>Disposing the shell disposes the engine host once and surfaces a dispose failure, with the dispose error's message, to the head.</summary>
    [Fact]
    public async Task DisposingTheShellSurfacesAnEngineHostDisposeFailureAfterDisposingItOnce()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var channel = new DisposeCountingChannel { DisposeFailure = new InvalidOperationException("engine dispose failed") };
            var shell = ParityShellHarness.CreateMain(channel);
            ParityShellHarness.Show(shell);

            var failure = await Should.ThrowAsync<InvalidOperationException>(() => shell.DisposeAsync().AsTask()).ConfigureAwait(true);

            failure.Message.ShouldContain("engine dispose failed");
            channel.DisposeCalls.ShouldBe(1);

            // A second dispose is a no-op: the engine is not disposed again and nothing throws.
            await shell.DisposeAsync().ConfigureAwait(true);
            channel.DisposeCalls.ShouldBe(1);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A runtime whose engine host fails to dispose has already stopped its stage observation and disposes it only once.</summary>
    [Fact]
    public async Task ARuntimeWhoseEngineFailsToDisposeNeverLeavesTheStagePumpRunning()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-1", "Agent one");
            var channel = new DisposeCountingChannel { DisposeFailure = new InvalidOperationException("engine dispose failed") };
            var stages = new EndingStageSource();
            var runtime = new AgentSessionRuntime(new AgentSessionRuntimeOptions
            {
                Row = fixture.Catalog.Rows[0],
                Prompts = channel,
                Approvals = new UnconnectedToolApprovalBridge(),
                Scope = new StageObservationScope
                {
                    SessionId = "session-1",
                    WorkspaceRoot = Environment.CurrentDirectory,
                    SessionStateDirectory = Path.Combine(Path.GetTempPath(), "ompos-shell-tests", "session-1"),
                },
                Stages = stages,
                Context = new ScriptedTaskContext(null),
            });
            runtime.StartObserving();
            await stages.Started.WaitAsync(TraceTimeout, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var failure = await Should.ThrowAsync<InvalidOperationException>(() => runtime.DisposeAsync().AsTask()).ConfigureAwait(true);

            failure.Message.ShouldBe("engine dispose failed");
            stages.Ended.IsCompleted.ShouldBeTrue("the stage pump ends before the engine is disposed");
            channel.DisposeCalls.ShouldBe(1);

            await runtime.DisposeAsync().ConfigureAwait(true);
            channel.DisposeCalls.ShouldBe(1, "a second dispose does nothing");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closing the main view releases the view only: the engine host belongs to the runtime, which the head disposes.</summary>
    [Fact]
    public async Task ClosingTheMainViewLeavesTheEngineHostToItsRuntime()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var channel = new DisposeCountingChannel();
            var shell = ParityShellHarness.CreateMain(channel);
            ParityShellHarness.Show(shell);

            shell.Main.Close();

            channel.DisposeCalls.ShouldBe(0, "closing the view does not dispose the engine");

            await shell.DisposeAsync().ConfigureAwait(true);

            channel.DisposeCalls.ShouldBe(1);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closing the view with an MCP host whose dispose fails traces the failure, with the dispose error's message.</summary>
    [Fact]
    public async Task ClosingTheMainViewTracesAnMcpHostDisposeFailure()
    {
        await using var session = TestAppBuilder.StartSession();
        var traced = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new CloseFailureListener(traced, McpMarker);
        Trace.Listeners.Add(listener);
        try
        {
            await session.RunAsync(async () =>
            {
                var fixture = ShellFixture.CreateWithSession();
                await using var fixtureScope = fixture.ConfigureAwait(true);
                var main = new MainView(fixture.Panel, Environment.CurrentDirectory, MotionPreference.Full, mcpHost: new DisposeFailingMcpHost());
                var window = new Window { Content = main, Width = 900, Height = 600 };
                window.Show();
                ShellHarness.Settle(window);
                main.Close();
                window.Close();
            }, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var message = await traced.Task.WaitAsync(TraceTimeout, TestContext.Current.CancellationToken).ConfigureAwait(true);
            message.ShouldContain("mcp dispose failed");
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    /// <summary>
    /// A send path that is never used and whose disposal is counted and may fail. The close path cancels only a turn in flight and this
    /// channel never has one, so it refuses both the prompt and the cancel.
    /// </summary>
    private sealed class DisposeCountingChannel : IPromptChannel
    {
        private int _disposeCalls;

        /// <summary>Gets how many times the channel was disposed.</summary>
        public int DisposeCalls => Volatile.Read(ref _disposeCalls);

        /// <summary>Gets or sets the failure the dispose raises, or null when it succeeds.</summary>
        public Exception? DisposeFailure { get; set; }

        /// <summary>Refuses every prompt: the shell is closed while idle, so none is submitted.</summary>
        /// <param name="prompt">The prompt text, which is never read.</param>
        /// <param name="cancellationToken">The cancellation token, which is never read.</param>
        /// <returns>Never returns; the call throws.</returns>
        public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This channel accepts no prompts.");

        /// <summary>Refuses every cancel: the channel never has a turn in flight.</summary>
        /// <param name="cancellationToken">The cancellation token, which is never read.</param>
        /// <returns>Never returns; the call throws.</returns>
        public Task CancelAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("This channel accepts no cancellations.");

        /// <summary>Counts the dispose and fails it the way a host whose teardown breaks does, when a failure is set.</summary>
        /// <returns>A value task that has faulted with the dispose failure, or a completed one.</returns>
        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCalls);
            return DisposeFailure is { } failure ? ValueTask.FromException(failure) : ValueTask.CompletedTask;
        }
    }

    /// <summary>A stage source that observes until it is cancelled and records when the observation started and when it ended.</summary>
    private sealed class EndingStageSource : IStageSource
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets a task that completes when the observation started.</summary>
        public Task Started => _started.Task.WaitAsync(CancellationToken.None);

        /// <summary>Gets a task that completes when the observation ended.</summary>
        public Task Ended => _ended.Task.WaitAsync(CancellationToken.None);

        /// <inheritdoc />
        public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(true);
            }
            finally
            {
                _ended.TrySetResult();
            }

            yield break;
        }
    }

    /// <summary>An MCP host with no tools whose disposal fails the way a registry whose teardown breaks does.</summary>
    private sealed class DisposeFailingMcpHost : IMcpRegistryHost
    {
        /// <inheritdoc />
        public IReadOnlyList<McpToolDescriptor> Tools => [];

        /// <inheritdoc />
        public IReadOnlyList<McpToolDescriptor> GetOfferedTools(string sessionId) => [];

        /// <inheritdoc />
        public bool IsEnabled(string sessionId, McpToolId tool) => false;

        /// <inheritdoc />
        public void Enable(string sessionId, McpToolId tool)
        {
        }

        /// <inheritdoc />
        public void Disable(string sessionId, McpToolId tool)
        {
        }

        /// <summary>Fails the dispose the way a registry whose teardown breaks does.</summary>
        /// <returns>A value task that has faulted with the dispose failure.</returns>
        public ValueTask DisposeAsync() => ValueTask.FromException(new InvalidOperationException("mcp dispose failed"));
    }

    /// <summary>A trace listener that completes once a close failure with the given marker is traced, with the traced text.</summary>
    /// <param name="traced">Completed with the first traced message that reports the failed close.</param>
    /// <param name="marker">The fragment the close continuation writes on that failed close.</param>
    private sealed class CloseFailureListener(TaskCompletionSource<string> traced, string marker) : TraceListener
    {
        /// <inheritdoc />
        public override void Write(string? message) => Record(message);

        /// <inheritdoc />
        public override void WriteLine(string? message) => Record(message);

        /// <inheritdoc />
        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message) =>
            Record(message);

        /// <summary>Completes the trace task when the text reports a failed close.</summary>
        /// <param name="text">The text the listener received.</param>
        private void Record(string? text)
        {
            if (text is not null && text.Contains(marker, StringComparison.Ordinal))
            {
                traced.TrySetResult(text);
            }
        }
    }
}
