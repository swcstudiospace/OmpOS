using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmpOS.Native.Views.RunControls;
using OmpOS.Presentation;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Runs;

namespace OmpOS.Native.Tests.RunControls;

/// <summary>Shared helpers for the run-control tests: shows a window with chosen dependencies, clicks, finds rows and waits for decisions.</summary>
internal static class RunControlsHarness
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    /// <summary>Shows a main window whose approval bridge and run input are the given dependencies.</summary>
    /// <param name="bridge">The approval bridge the window routes decisions through.</param>
    /// <param name="runInput">The run input the window routes steers and follow-ups through.</param>
    /// <returns>The shown window.</returns>
    public static MainWindow Show(IToolApprovalBridge bridge, IRunInput runInput)
    {
        var window = new MainWindow(bridge, runInput, MotionPreference.System);
        window.Show();
        ShellHarness.Settle(window);
        return window;
    }

    /// <summary>Shows a main window built with the production defaults, which connect no approval service and no run input.</summary>
    /// <returns>The shown window.</returns>
    public static MainWindow ShowDefaults()
    {
        var window = new MainWindow(MotionPreference.System);
        window.Show();
        ShellHarness.Settle(window);
        return window;
    }

    /// <summary>Adds one pending request to the window's run controls and settles the layout.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="tool">The tool that needs approval.</param>
    /// <param name="arguments">The arguments summary.</param>
    /// <returns>The request the window now shows.</returns>
    public static PendingToolApproval AddRequest(MainWindow window, string requestId, string tool = "bash", string arguments = "command: ls")
    {
        var approval = new PendingToolApproval("session-1", requestId, tool, arguments);
        window.RunControls.Approvals.Add(approval);
        ShellHarness.Settle(window);
        return approval;
    }

    /// <summary>Raises the click of a button, as a pointer press would.</summary>
    /// <param name="button">The button to click.</param>
    public static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>Gets the first button with the given accessible name.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="name">The accessible name.</param>
    /// <returns>The first matching button.</returns>
    public static Button Named(MainWindow window, string name) =>
        window.GetVisualDescendants().OfType<Button>().First(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    /// <summary>Gets the follow-up control of the composer.</summary>
    /// <param name="window">The shown window.</param>
    /// <returns>The follow-up button.</returns>
    public static Button FollowUp(MainWindow window) => ShellHarness.Composer(window).FindControl<Button>("FollowUpButton")!;

    /// <summary>Gets the composer's pending-input label.</summary>
    /// <param name="window">The shown window.</param>
    /// <returns>The label.</returns>
    public static TextBlock PendingLabel(MainWindow window) => ShellHarness.Composer(window).FindControl<TextBlock>("PendingLabel")!;

    /// <summary>Gets the text of the composer's pending-input label.</summary>
    /// <param name="window">The shown window.</param>
    /// <returns>The label text, or an empty string when no label is set.</returns>
    public static string PendingText(MainWindow window) => PendingLabel(window).Text ?? string.Empty;

    /// <summary>Gets the run-controls list.</summary>
    /// <param name="window">The shown window.</param>
    /// <returns>The list view.</returns>
    public static RunControlsView RunControlsList(MainWindow window) => window.GetVisualDescendants().OfType<RunControlsView>().Single();

    /// <summary>Gets one approval row in the order the requests were added.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="index">The zero-based row index.</param>
    /// <returns>The row.</returns>
    public static ToolApprovalRowView Row(MainWindow window, int index = 0) =>
        window.GetVisualDescendants().OfType<ToolApprovalRowView>().ElementAt(index);

    /// <summary>Gets the text of a named text block inside an approval row.</summary>
    /// <param name="row">The row.</param>
    /// <param name="element">The name of the text block.</param>
    /// <returns>The text, or an empty string when the block has none.</returns>
    public static string RowText(ToolApprovalRowView row, string element) => row.FindControl<TextBlock>(element)!.Text ?? string.Empty;

    /// <summary>Waits until a condition holds on the UI thread, pumping dispatcher work while it waits.</summary>
    /// <param name="condition">The condition to wait for.</param>
    /// <param name="description">What the test waits for, for the timeout message.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    /// <exception cref="TimeoutException">The condition did not hold within the wait limit.</exception>
    public static async Task UntilAsync(Func<bool> condition, string description, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for: {description}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(true);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
