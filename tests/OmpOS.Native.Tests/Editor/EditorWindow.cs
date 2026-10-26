using Avalonia.Controls;

namespace OmpOS.Native.Tests.Editor;

/// <summary>
/// A shown editor window. Disposing it closes the window, which detaches the editor and stops its TextMate tokenizer
/// before the next test starts, so no background tokenization outlives its own test.
/// </summary>
internal sealed class EditorWindow : Window, IDisposable
{
    /// <summary>Closes the window.</summary>
    public void Dispose() => Close();
}
