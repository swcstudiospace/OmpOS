using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.VisualTree;
using OmpOS.Native.Views.RunControls;
using OmpOS.Presentation.Approvals;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.RunControls;

/// <summary>
/// A decision that faults is never dropped (M-4, D5). While the row is attached the failure shows on the row. After the row has
/// detached there is no row to show it on, so the failure reaches the trace listener instead.
/// </summary>
public sealed class ToolApprovalRowTraceTests
{
    /// <summary>The upper bound the test waits for the trace; a build that drops the fault reaches it and fails.</summary>
    private static readonly TimeSpan TraceTimeout = TimeSpan.FromSeconds(10);

    /// <summary>A decision fault while the row is attached shows the failure on the row's status text.</summary>
    [Fact]
    public async Task ADecisionFaultWhileTheRowIsAttachedShowsTheFailureOnTheRow()
    {
        await using var session = TestAppBuilder.StartSession();
        var bridge = new FaultingBridge();
        await session.RunAsync(async () =>
        {
            var window = RunControlsHarness.Show(bridge, new RecordingRunInput());
            RunControlsHarness.AddRequest(window, "request-fault-attached");
            var row = RunControlsHarness.Row(window);
            var expected = $"{MainView.FaultedStatus} The bridge refused the record.";

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
            bridge.FailOnBackgroundThread(new InvalidOperationException("The bridge refused the record."));
            await RunControlsHarness.UntilAsync(() => string.Equals(RunControlsHarness.RowText(row, "StatusText"), expected, StringComparison.Ordinal), "the decision fault on the row", TestContext.Current.CancellationToken).ConfigureAwait(true);

            RunControlsHarness.RowText(row, "StatusText").ShouldBe(expected);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// A decision that faults after its row has left the window is traced and not dropped. The decision call is not cancelled by
    /// the removal, so the coordinator rethrows the bridge fault into the task the row follows; the row traces it before it posts
    /// the settlement the detached attachment cancels. The listener is a plain <see cref="TraceListener"/> added on the UI thread
    /// after the session started, so the startup listener the application installs cannot replace it.
    /// </summary>
    [Fact]
    public async Task ADecisionFaultAfterTheRowDetachedIsTraced()
    {
        await using var session = TestAppBuilder.StartSession();
        var bridge = new FaultingBridge();
        var traced = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new MarkerListener(traced, "could not record the tool-approval decision request-fault-detached");
        try
        {
            await session.RunAsync(async () =>
            {
                Trace.Listeners.Add(listener);
                var window = RunControlsHarness.Show(bridge, new RecordingRunInput());
                var approval = RunControlsHarness.AddRequest(window, "request-fault-detached");

                RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
                ShellHarness.MainViewOf(window).RunControls.Approvals.Remove(approval);
                ShellHarness.Settle(window);
                window.GetVisualDescendants().OfType<ToolApprovalRowView>().ShouldBeEmpty();

                bridge.FailOnBackgroundThread(new InvalidOperationException("The bridge refused the late record."));
                var line = await traced.Task.WaitAsync(TraceTimeout, TestContext.Current.CancellationToken).ConfigureAwait(true);

                line.ShouldContain("The bridge refused the late record.");
            }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    /// <summary>A trace listener that completes once a line containing the marker is written, with that line.</summary>
    /// <param name="traced">Completed with the first written line that contains the marker.</param>
    /// <param name="marker">The fragment the row's decision-fault trace contains.</param>
    private sealed class MarkerListener(TaskCompletionSource<string> traced, string marker) : TraceListener
    {
        /// <inheritdoc/>
        public override void Write(string? message) => Record(message);

        /// <inheritdoc/>
        public override void WriteLine(string? message) => Record(message);

        /// <summary>Completes the task when the text contains the marker.</summary>
        /// <param name="text">The text the listener received.</param>
        private void Record(string? text)
        {
            if (text is not null && text.Contains(marker, StringComparison.Ordinal))
            {
                traced.TrySetResult(text);
            }
        }
    }

    /// <summary>A bridge that answers each decision only when the test fails it, from a background thread.</summary>
    private sealed class FaultingBridge : IToolApprovalBridge
    {
        private readonly ConcurrentQueue<TaskCompletionSource<ToolApprovalOutcome>> _answers = new();

        /// <inheritdoc/>
        public Task<ToolApprovalOutcome> DecideAsync(string sessionId, string requestId, ToolApprovalDecision decision, CancellationToken cancellationToken)
        {
            var answer = new TaskCompletionSource<ToolApprovalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            _answers.Enqueue(answer);
            return answer.Task;
        }

        /// <summary>Faults the oldest pending decision on a background thread.</summary>
        /// <param name="fault">The exception the decision faults with.</param>
        public void FailOnBackgroundThread(Exception fault)
        {
            if (!_answers.TryDequeue(out var answer))
            {
                throw new InvalidOperationException("No decision is pending.");
            }

            var settler = new Thread(() => answer.SetException(fault))
            {
                IsBackground = true,
            };
            settler.Start();
        }
    }
}
