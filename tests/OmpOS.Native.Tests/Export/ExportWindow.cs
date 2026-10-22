using Avalonia.Controls;

namespace OmpOS.Native.Tests.Export;

/// <summary>A shown export window. Disposing it closes the window.</summary>
internal sealed class ExportWindow : Window, IDisposable
{
    /// <summary>Closes the window.</summary>
    public void Dispose() => Close();
}
