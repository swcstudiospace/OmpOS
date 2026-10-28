namespace Autonogrammer.Harness.Secrets;

/// <summary>
/// The abstraction for every channel that text can leave the process through: logs, transcripts, crash reports and
/// telemetry. Canary tests in the test project verify that a registered secret never reaches one of them.
/// </summary>
public interface ISecretSink
{
    /// <summary>Writes one piece of text to a channel.</summary>
    /// <param name="channel">The channel the text is bound for.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// A task that completes once the text is written. It faults or is cancelled when the text cannot be written.
    /// </returns>
    /// <remarks>Every refusal is reported through the returned task; implementations must not throw synchronously.</remarks>
    Task WriteAsync(SecretSinkChannel channel, string text, CancellationToken cancellationToken);
}
