using Avalonia.Controls;

namespace OmpOS.Native.Tests.InlineEditing;

/// <summary>A shown inline edit window. Disposing it closes the window, so no view outlives its test.</summary>
internal sealed class InlineWindow : Window, IDisposable
{
    /// <summary>Closes the window.</summary>
    public void Dispose() => Close();
}
