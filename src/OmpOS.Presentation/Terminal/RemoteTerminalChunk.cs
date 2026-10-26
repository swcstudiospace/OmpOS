namespace OmpOS.Presentation.Terminal;

/// <summary>One numbered piece of remote terminal output.</summary>
/// <param name="Sequence">The position of the chunk in the terminal's output stream. The first chunk is 1 and each next chunk is one greater.</param>
/// <param name="Text">The output text, including any terminal escape sequences, exactly as the remote terminal produced it.</param>
public sealed record RemoteTerminalChunk(long Sequence, string Text);
