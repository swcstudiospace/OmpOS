using System.Collections.Concurrent;
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
    /// <summary>A decision that completes after the row detached records the outcome, and the row does not update.</summary>
    [Fact]
    public async Task ASettlementAfterTheRowDetachedLeavesTheRowUntouched()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var bridge = new GatedBridge();
            var window = RunControlsHarness.Show(bridge, new RecordingRunInput());
            var approval = RunControlsHarness.AddRequest(window, "request-1");
            var row = RunControlsHarness.Row(window);

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
            window.RunControls.Approvals.Remove(approval);
            ShellHarness.Settle(window);
            window.GetVisualDescendants().OfType<ToolApprovalRowView>().ShouldBeEmpty();

            bridge.CompleteOnBackgroundThread(ToolApprovalOutcome.Accepted);
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the late settlement", TestContext.Current.CancellationToken).ConfigureAwait(true);

            // Give any work the row posted for this settlement the chance to run before the checks.
            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(window);

            approval.Status.ShouldBe(ToolApprovalStatus.Denied);
            RunControlsHarness.RowText(row, "StatusText").ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
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

        /// <summary>Completes the oldest pending decision on a background thread.</summary>
        /// <param name="outcome">The outcome the bridge reports.</param>
        public void CompleteOnBackgroundThread(ToolApprovalOutcome outcome)
        {
            if (!_answers.TryDequeue(out var answer))
            {
                throw new InvalidOperationException("No decision is pending.");
            }

            var settler = new Thread(() => answer.SetResult(outcome))
            {
                IsBackground = true,
            };
            settler.Start();
        }
    }
}
