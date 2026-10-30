using Avalonia.Controls;

namespace Autonogrammer.Native.Tests.Diff;

/// <summary>A shown diff review window. Disposing it closes the window, so no view outlives its test.</summary>
internal sealed class DiffWindow : Window, IDisposable
{
    /// <summary>Closes the window.</summary>
    public void Dispose() => Close();
}
