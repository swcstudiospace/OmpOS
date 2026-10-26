namespace OmpOS.Native.Views.AgentPanel;

/// <summary>The arguments of <see cref="ComposerView.StatusMessage"/>: a sentence for the session status line.</summary>
internal sealed class ComposerStatusEventArgs : EventArgs
{
    /// <summary>Creates the arguments for one status sentence.</summary>
    /// <param name="message">The sentence, for example the queue-full text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
    public ComposerStatusEventArgs(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Message = message;
    }

    /// <summary>Gets the sentence for the session status line.</summary>
    public string Message { get; }
}
