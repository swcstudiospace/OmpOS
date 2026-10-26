using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.VisualTree;
using OmpOS.Native.Views.RunControls;
using OmpOS.Presentation.Approvals;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.RunControls;

/// <summary>
/// A decision that completes after its row has left the window. The request is still settled in the model, but the row is gone,
/// so the settlement must not touch the row's controls, and nothing may throw.
/// </summary>
public sealed class DetachedSettlementTests
{
    /// <summary>The upper bound the test waits for the trace; a build that drops the fault reaches it and fails loudly.</summary>
    private static readonly TimeSpan TraceTimeout = TimeSpan.FromSeconds(10);

    /// <summary>A decision that completes after the row detached records the outcome, and the row does not update.</summary>
    [Fact]
    public async Task ASettlementAfterTheRowDetachedLeavesTheRowUntouched()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var bridge = new GatedBridge();
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, "request-1");
            var row = RunControlsHarness.Row(window);

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
            ShellHarness.RuntimeOf(window).RunControls.Approvals.Remove(approval);
            ShellHarness.Settle(window);
            window.GetVisualDescendants().OfType<ToolApprovalRowView>().ShouldBeEmpty();

            bridge.CompleteOnBackgroundThread(ToolApprovalOutcome.Accepted);
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the late settlement", TestContext.Current.CancellationToken).ConfigureAwait(true);

            // The settlement has happened (the model changed). Flush the work the row could have posted for it before the checks.
            await RunControlsHarness.FlushAsync(window, TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Denied);
            RunControlsHarness.RowText(row, "StatusText").ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// A decision that faults after the row detached does not reach the row either. The trace of the fault is the positive signal that the
    /// row's settlement continuation ran, so the flush that follows it covers the post the continuation makes next, and a row that
    /// still took the late fault would show the faulted status.
    /// </summary>
    [Fact]
    public async Task AFaultAfterTheRowDetachedLeavesTheRowUntouched()
    {
        await using var session = TestAppBuilder.StartSession();
        var traced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new TracedListener(traced, "request-detached-untouched");
        try
        {
            await session.RunAsync(async () =>
            {
                Trace.Listeners.Add(listener);
                var bridge = new GatedBridge();
                var window = RunControlsHarness.Show(bridge);
                var approval = RunControlsHarness.AddRequest(window, "request-detached-untouched");
                var row = RunControlsHarness.Row(window);

                RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
                ShellHarness.RuntimeOf(window).RunControls.Approvals.Remove(approval);
                ShellHarness.Settle(window);
                window.GetVisualDescendants().OfType<ToolApprovalRowView>().ShouldBeEmpty();

                bridge.FailOnBackgroundThread(new InvalidOperationException("The bridge refused the late record."));
                await traced.Task.WaitAsync(TraceTimeout, TestContext.Current.CancellationToken).ConfigureAwait(true);
                await RunControlsHarness.FlushAsync(window, TestContext.Current.CancellationToken).ConfigureAwait(true);

                approval.Status.ShouldBe(ToolApprovalStatus.Pending);
                RunControlsHarness.RowText(row, "StatusText").ShouldBeEmpty();
            }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    /// <summary>A trace listener that completes once a line containing the marker is written.</summary>
    /// <param name="traced">Completed when a written line contains the marker.</param>
    /// <param name="marker">The fragment the row's decision-fault trace contains.</param>
    private sealed class TracedListener(TaskCompletionSource traced, string marker) : TraceListener
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
                traced.TrySetResult();
            }
        }
    }

    /// <summary>A bridge that answers each decision only when the test releases it, from a background thread.</summary>
    private sealed class GatedBridge : IToolApprovalBridge
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
        public void FailOnBackgroundThread(Exception fault) => ReleaseOnBackgroundThread(answer => answer.SetException(fault));

        /// <summary>Completes the oldest pending decision on a background thread.</summary>
        /// <param name="outcome">The outcome the bridge reports.</param>
        public void CompleteOnBackgroundThread(ToolApprovalOutcome outcome) => ReleaseOnBackgroundThread(answer => answer.SetResult(outcome));

        private void ReleaseOnBackgroundThread(Action<TaskCompletionSource<ToolApprovalOutcome>> release)
        {
            if (!_answers.TryDequeue(out var answer))
            {
                throw new InvalidOperationException("No decision is pending.");
            }

            var settler = new Thread(() => release(answer))
            {
                IsBackground = true,
            };
            settler.Start();
        }
    }
}
