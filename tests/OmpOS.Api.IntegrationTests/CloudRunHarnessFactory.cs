using System.Runtime.CompilerServices;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// An <see cref="IHarnessSessionFactory"/> for hosted run tests. Every session replays the same events and then completes. When gated,
/// a session announces that its harness started the prompt through <see cref="Entered"/> and waits for <see cref="Release"/> before it
/// emits anything, so a test observes the run while the harness is mid-prompt. A cancelled prompt stops at the gate with the cancellation.
/// </summary>
internal sealed class CloudRunHarnessFactory : IHarnessSessionFactory
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IReadOnlyList<HarnessEvent> _events;

    /// <summary>Initializes a new instance of the <see cref="CloudRunHarnessFactory"/> class.</summary>
    /// <param name="events">The events every prompt replays before its completion.</param>
    /// <param name="gated">Whether a prompt waits for <see cref="Release"/> before it emits.</param>
    public CloudRunHarnessFactory(IReadOnlyList<HarnessEvent> events, bool gated)
    {
        ArgumentNullException.ThrowIfNull(events);
        _events = events;
        if (!gated)
        {
            _gate.SetResult();
        }
    }

    /// <summary>Gets a task that completes when a harness session starts its prompt.</summary>
    public Task Entered => _entered.Task.WaitAsync(CancellationToken.None);

    /// <summary>Releases every prompt waiting at the gate.</summary>
    public void Release() => _ = _gate.TrySetResult();

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return new Session(this);
    }

    private sealed class Session : IOmpSession
    {
        private readonly CloudRunHarnessFactory _owner;

        public Session(CloudRunHarnessFactory owner)
        {
            _owner = owner;
        }

        public HarnessState State => HarnessState.Ready;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
            string prompt,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            ArgumentNullException.ThrowIfNull(options);
            _ = _owner._entered.TrySetResult();
            await _owner._gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            foreach (var evt in _owner._events)
            {
                yield return evt;
            }

            yield return new CompletedEvent("end_turn");
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
