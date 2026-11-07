using Autonogrammer.Presentation.Dispatch;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Dispatch;

/// <summary>The Remote task running strip. Stop raises a request; the head stops the task.</summary>
internal partial class RemoteTaskStripView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="RemoteTaskStripView"/> class.</summary>
    public RemoteTaskStripView()
    {
        InitializeComponent();
        FromText.Text = DispatchCopy.RemoteTaskFromLabel;
        StopButton.Content = DispatchCopy.StopLabel;
        StopButton.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the user asks to stop the remote task.</summary>
    public event EventHandler? StopRequested;
}
