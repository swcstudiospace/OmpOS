using OmpOS.Harness.Abstractions;

namespace OmpOS.Native.Engine;

/// <summary>
/// The send path of a composed engine, as the main view uses it: a prompt is submitted and its events stream back in order, and Stop
/// cancels the turn in flight. The host that composes the engine supplies the implementation, and the main view disposes it when it closes.
/// </summary>
public interface IPromptChannel : IAsyncDisposable
{
    /// <summary>Submits one prompt as a user turn and yields the events the host reports, in order.</summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="cancellationToken">Cancels the consumer side of the stream.</param>
    /// <returns>The host's event stream for the turn.</returns>
    IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken);

    /// <summary>
    /// Submits one prompt that carries resolved @-mentions as embedded resources. A channel that cannot send resources keeps this default, which
    /// sends the text alone, so a mention never reaches an engine that cannot take it.
    /// </summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="options">The options of the submission, notably the mention resources.</param>
    /// <param name="cancellationToken">Cancels the consumer side of the stream.</param>
    /// <returns>The host's event stream for the turn.</returns>
    IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, PromptOptions options, CancellationToken cancellationToken) =>
        SubmitAsync(prompt, cancellationToken);

    /// <summary>Cancels the in-flight turn of the composed host, if any.</summary>
    /// <param name="cancellationToken">Cancels the request itself.</param>
    /// <returns>A task that completes when the host has accepted the cancellation.</returns>
    Task CancelAsync(CancellationToken cancellationToken);
}
