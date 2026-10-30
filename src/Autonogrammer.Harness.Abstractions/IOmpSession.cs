namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// One omp conversation as seen by the harness. A session serves one prompt at a time;
/// every lifecycle move is checked against <see cref="HarnessStateMachine"/>.
/// </summary>
public interface IOmpSession : IAsyncDisposable
{
    /// <summary>Starts a new engine session and completes the handshake.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    Task CreateAsync(CancellationToken cancellationToken);

    /// <summary>Resumes an existing engine session by identifier.</summary>
    /// <param name="sessionId">The identifier of the session to resume.</param>
    /// <param name="cancellationToken">Cancels the resume.</param>
    Task ResumeAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Submits a prompt and streams the resulting events in order.</summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="options">Prompt options.</param>
    /// <param name="cancellationToken">Cancels the consumer side of the stream.</param>
    /// <returns>
    /// The event stream. Each turn ends with exactly one terminal event: a <see cref="CompletedEvent"/> on success
    /// or an <see cref="ErrorEvent"/> on failure. An <see cref="ErrorEvent"/> ends the stream.
    /// <see cref="DiagnosticEvent"/> is non-terminal and does not end the turn.
    /// </returns>
    IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, CancellationToken cancellationToken);

    /// <summary>Requests cancellation of the in-flight prompt, if any.</summary>
    /// <param name="cancellationToken">Cancels the request itself.</param>
    Task CancelAsync(CancellationToken cancellationToken);

    /// <summary>Gets the current lifecycle state.</summary>
    HarnessState State { get; }

    /// <summary>Gets the capabilities advertised by the engine. Empty for in-memory sessions.</summary>
    IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; }
}
