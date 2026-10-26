using System.Collections.Concurrent;
using OmpOS.Presentation.Approvals;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.RunControls;

/// <summary>
/// A decision that faults while the row is attached is shown on the row (D5). The trace for a fault after the row has detached is not
/// covered by a test here; see the phase summary.
/// </summary>
public sealed class ToolApprovalRowTraceTests
{
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
