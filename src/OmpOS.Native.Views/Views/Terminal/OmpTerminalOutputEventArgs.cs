namespace OmpOS.Native.Views.Terminal;

/// <summary>A piece of terminal output from omp.</summary>
internal sealed class OmpTerminalOutputEventArgs(string text) : EventArgs
{
    /// <summary>Gets the output text.</summary>
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));
}
