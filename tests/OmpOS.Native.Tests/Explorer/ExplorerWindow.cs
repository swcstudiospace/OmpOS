using Avalonia.Controls;

namespace OmpOS.Native.Tests.Explorer;

/// <summary>A shown explorer window. Disposing it closes the window, so no view outlives its test.</summary>
internal sealed class ExplorerWindow : Window, IDisposable
{
    /// <summary>Closes the window.</summary>
    public void Dispose() => Close();
}
