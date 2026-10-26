using OmpOS.Presentation.Approvals;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.RunControls;

/// <summary>
/// A decision that the engine settles on a background thread. The row is a UI object, so the outcome must reach it on the UI thread,
/// and the row must then show the outcome the bridge reported.
/// </summary>
public sealed class BackgroundSettlementTests
{
    /// <summary>A request the bridge settles on a background thread shows the outcome on the row, on the UI thread.</summary>
    [Fact]
    public async Task ARequestSettledOnABackgroundThreadShowsItsOutcome()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var bridge = new BackgroundSettlingBridge(ToolApprovalOutcome.Accepted);
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, "request-1");

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the background settlement", TestContext.Current.CancellationToken).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => !RunControlsHarness.Named(window, "Approve tool request").IsEnabled, "the request to settle", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Denied);
            bridge.SettledOnBackgroundThread.ShouldBeTrue();
            RunControlsHarness.RowText(RunControlsHarness.Row(window), "StatusText").ShouldBe(approval.StatusMessage);
            approval.StatusMessage.ShouldNotBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A bridge whose answer is completed on a dedicated background thread, as an engine answering asynchronously would.</summary>
    private sealed class BackgroundSettlingBridge(ToolApprovalOutcome outcome) : IToolApprovalBridge
    {
        /// <summary>Gets whether the answer was completed on a thread other than the caller's.</summary>
        public bool SettledOnBackgroundThread { get; private set; }

        /// <inheritdoc/>
        public Task<ToolApprovalOutcome> DecideAsync(string sessionId, string requestId, ToolApprovalDecision decision, CancellationToken cancellationToken)
        {
            var caller = Environment.CurrentManagedThreadId;
            var answer = new TaskCompletionSource<ToolApprovalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            var settler = new Thread(() =>
            {
                SettledOnBackgroundThread = Environment.CurrentManagedThreadId != caller;
                answer.SetResult(outcome);
            })
            {
                IsBackground = true,
            };
            settler.Start();
            return answer.Task;
        }
    }
}
