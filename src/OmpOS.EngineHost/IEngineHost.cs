using OmpOS.Harness.Abstractions;

namespace OmpOS.EngineHost;

/// <summary>
/// The seam every surface head programs against. Desktop heads bind <see cref="InProcessEngineHost"/>;
/// mobile heads bind <see cref="RemoteEngineHost"/> and call <see cref="RemoteEngineHost.ReconnectAsync"/>
/// on foreground resume.
/// </summary>
public interface IEngineHost
{
    /// <summary>Gets the descriptor this host reports (exactly as constructed).</summary>
    EngineHostDescriptor Descriptor { get; }

    /// <summary>Submits a prompt and streams the resulting events in order.</summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="options">Prompt options.</param>
    /// <param name="cancellationToken">Cancels the consumer side of the stream.</param>
    /// <returns>The event stream, ending with a <see cref="CompletedEvent"/> on success.</returns>
    IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, CancellationToken cancellationToken);

    /// <summary>Requests cancellation of the in-flight prompt, if any. A host with no active prompt completes immediately.</summary>
    /// <param name="cancellationToken">Cancels the request itself.</param>
    Task CancelAsync(CancellationToken cancellationToken);
}
