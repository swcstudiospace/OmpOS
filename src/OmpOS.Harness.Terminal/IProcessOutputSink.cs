namespace OmpOS.Harness.Terminal;

/// <summary>
/// Receives the output of an approved run as it arrives, so a display can show it while the command is still running.
/// The runner calls <see cref="Write"/> from one background thread at a time, in output order. The sink must not block for long.
/// </summary>
public interface IProcessOutputSink
{
    /// <summary>
    /// Receives the next piece of output, decoded as UTF-8 and containing terminal control sequences unchanged.
    /// </summary>
    /// <param name="text">The output text. It is never empty.</param>
    void Write(string text);
}
