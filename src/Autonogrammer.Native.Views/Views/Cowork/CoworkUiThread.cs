using Avalonia.Threading;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>Runs a view refresh on the UI thread. A model may raise its change on a background thread, so a refresh posts when it is not on the UI thread.</summary>
internal static class CoworkUiThread
{
    /// <summary>Runs the action now on the UI thread, or posts it there.</summary>
    /// <param name="action">The refresh.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
