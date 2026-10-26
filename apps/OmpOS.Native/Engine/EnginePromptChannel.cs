using OmpOS.Harness.Abstractions;

namespace OmpOS.Native.Engine;

/// <summary>
/// The send path of the composed engine. A prompt is submitted to the engine's host and its events stream back in order; Stop cancels
/// the in-flight turn through the same host. The engine is composed on the first send, never before.
/// </summary>
internal sealed class EnginePromptChannel(Lazy<EngineComposition> engine) : IPromptChannel
{
    private readonly Lazy<EngineComposition> _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    /// <summary>Submits one prompt as a user turn and yields the events the host reports, in order.</summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="cancellationToken">Cancels the consumer side of the stream.</param>
    /// <returns>The host's event stream for the turn.</returns>
    public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken) =>
        _engine.Value.Host.SubmitPromptAsync(prompt, new PromptOptions(), cancellationToken);

    /// <summary>Cancels the in-flight turn of the composed host, if any.</summary>
    /// <param name="cancellationToken">Cancels the request itself.</param>
    /// <returns>A task that completes when the host has accepted the cancellation.</returns>
    public Task CancelAsync(CancellationToken cancellationToken) => _engine.Value.Host.CancelAsync(cancellationToken);

    /// <summary>Disposes the composed host when it was composed. Disposing a channel whose engine was never composed composes nothing.</summary>
    /// <returns>A task that completes when the host is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_engine.IsValueCreated)
        {
            await _engine.Value.Host.DisposeAsync().ConfigureAwait(false);
        }
    }
}
