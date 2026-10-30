using System.Runtime.CompilerServices;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.SessionRuns.Tests;

/// <summary>
/// Harness factory that records prompts and hands out sessions replaying a fixed script. The optional hook runs after each
/// event has been consumed, so a test can observe the session between a permission request and the rest of the stream.
/// </summary>
internal sealed class ScriptedHarnessFactory : IHarnessSessionFactory
{
    private readonly IReadOnlyList<HarnessEvent> _events;
    private readonly Exception? _submitFailure;
    private readonly Func<HarnessEvent, Task>? _afterEvent;
    private readonly List<string> _prompts = [];

    /// <summary>Initializes a new instance of the <see cref="ScriptedHarnessFactory"/> class.</summary>
    /// <param name="events">The events each session streams, in order.</param>
    /// <param name="submitFailure">The exception each session throws when the prompt is submitted, or null.</param>
    /// <param name="afterEvent">Runs after each streamed event is consumed, or null.</param>
    public ScriptedHarnessFactory(IReadOnlyList<HarnessEvent> events, Exception? submitFailure = null, Func<HarnessEvent, Task>? afterEvent = null)
    {
        _events = events;
        _submitFailure = submitFailure;
        _afterEvent = afterEvent;
    }

    /// <summary>Gets the prompts passed to <see cref="CreateSession"/>, in order.</summary>
    public IReadOnlyList<string> Prompts => _prompts;

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        _prompts.Add(prompt);
        return new ScriptedSession(_events, _submitFailure, _afterEvent);
    }

    /// <summary>Harness session that streams a fixed event script, or fails on submit when scripted to.</summary>
    private sealed class ScriptedSession : IOmpSession
    {
        private readonly IReadOnlyList<HarnessEvent> _events;
        private readonly Exception? _submitFailure;
        private readonly Func<HarnessEvent, Task>? _afterEvent;

        public ScriptedSession(IReadOnlyList<HarnessEvent> events, Exception? submitFailure, Func<HarnessEvent, Task>? afterEvent)
        {
            _events = events;
            _submitFailure = submitFailure;
            _afterEvent = afterEvent;
        }

        /// <inheritdoc />
        public HarnessState State { get; private set; } = HarnessState.Disconnected;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <inheritdoc />
        public Task CreateAsync(CancellationToken cancellationToken)
        {
            State = HarnessState.Ready;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The scripted session cannot resume an engine session.");

        /// <inheritdoc />
        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
            string prompt,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_submitFailure is { } failure)
            {
                throw failure;
            }

            foreach (var evt in _events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return evt;
                if (_afterEvent is not null)
                {
                    await _afterEvent(evt).ConfigureAwait(true);
                }
            }
        }

        /// <inheritdoc />
        public Task CancelAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("The scripted session has no in-flight prompt to cancel.");

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            State = HarnessState.Disposed;
            return ValueTask.CompletedTask;
        }
    }
}
