using Avalonia.Controls;

namespace Autonogrammer.Native.Tests.Completion;

/// <summary>A shown completion window. Disposing it closes the window.</summary>
internal sealed class CompletionWindow : Window, IDisposable
{
    /// <summary>Closes the window.</summary>
    public void Dispose() => Close();
}
