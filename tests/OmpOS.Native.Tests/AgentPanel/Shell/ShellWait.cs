using Avalonia.Threading;
using OmpOS.Native.Views.Sessions;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>Waits for what a shell does in the background: a turn that ends, a stage that folds. Polling is bounded by a deadline, never a fixed sleep.</summary>
internal static class ShellWait
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    /// <summary>Lets the dispatcher and the layout run until the condition holds.</summary>
    /// <param name="window">The shown window, settled on every poll.</param>
    /// <param name="condition">What must become true.</param>
    /// <param name="because">What the wait was for, named in the failure.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    /// <exception cref="TimeoutException">The condition did not hold within ten seconds.</exception>
    public static async Task UntilAsync(MainWindow window, Func<bool> condition, string because, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Limit);
        while (!condition())
        {
            if (deadline.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for: {because}");
            }

            ShellHarness.Settle(window);
            await Task.Yield();
            await Task.Delay(TimeSpan.FromMilliseconds(5), CancellationToken.None).ConfigureAwait(true);
        }

        ShellHarness.Settle(window);
    }

    /// <summary>Lets the dispatcher run until the condition holds, with no window to settle.</summary>
    /// <param name="condition">What must become true.</param>
    /// <param name="because">What the wait was for, named in the failure.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    /// <exception cref="TimeoutException">The condition did not hold within ten seconds.</exception>
    public static async Task UntilAsync(Func<bool> condition, string because, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Limit);
        while (!condition())
        {
            if (deadline.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for: {because}");
            }

            Dispatcher.UIThread.RunJobs();
            await Task.Delay(TimeSpan.FromMilliseconds(5), CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <summary>Waits until the runtime's run ended: its composer no longer streams.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="runtime">The runtime whose run is awaited.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the run ended.</returns>
    public static Task RunEndedAsync(MainWindow window, AgentSessionRuntime runtime, CancellationToken cancellationToken) =>
        UntilAsync(window, () => !runtime.Composer.IsStreaming, "the run to end", cancellationToken);
}
