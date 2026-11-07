using Autonogrammer.Presentation.Dispatch;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Dispatch;

/// <summary>
/// The Dispatch page. It binds <see cref="DispatchPageModel"/> only: the devices or their one phase on the polite status line, the failures
/// (an unreachable service and each failed command) on the assertive line, the command rows, and this desktop's pairing when the head has
/// one. Without a local device it says to pair in the desktop app.
/// </summary>
internal partial class DispatchPageView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="DispatchPageView"/> class.</summary>
    public DispatchPageView()
    {
        InitializeComponent();
        PageHeading.Text = DispatchCopy.PageHeading;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is DispatchPageModel page)
        {
            PairText.Text = page.PairThisDevice is { } pair ? pair.StatusText : DispatchCopy.PairInDesktopApp;
            FailureText.Text = FailuresOf(page);
        }
        else
        {
            PairText.Text = DispatchCopy.PairInDesktopApp;
            FailureText.Text = string.Empty;
        }
    }

    private static string FailuresOf(DispatchPageModel page)
    {
        var failures = page.Commands
            .Where(command => command.Phase == DispatchPhase.Failed)
            .Select(command => command.StatusText)
            .ToList();
        if (page.Devices.Phase == DispatchPhase.Unreachable)
        {
            failures.Insert(0, page.Devices.StatusText);
        }

        return string.Join(Environment.NewLine, failures);
    }
}
