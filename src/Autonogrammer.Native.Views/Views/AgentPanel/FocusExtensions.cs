using Avalonia.Controls;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>Focus requests for controls that may not be laid out yet.</summary>
internal static class FocusExtensions
{
    /// <summary>
    /// Moves keyboard focus to the control. A control that was shown a moment ago cannot take focus before its first layout pass, so a refused
    /// request is repeated once at <see cref="DispatcherPriority.Input"/>, which runs after layout.
    /// </summary>
    /// <param name="control">The control to focus. Null does nothing.</param>
    public static void FocusWhenReady(this Control? control)
    {
        if (control is null || control.Focus())
        {
            return;
        }

        Dispatcher.UIThread.Post(() => control.Focus(), DispatcherPriority.Input);
    }
}
