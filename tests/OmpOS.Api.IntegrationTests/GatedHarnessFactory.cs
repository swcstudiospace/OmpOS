using System.Runtime.CompilerServices;
using OmpOS.Api.Harness;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// An <see cref="IHarnessSessionFactory"/> whose sessions block mid-stream until released,
/// making the PromptInFlight (409) window deterministic.
/// </summary>
internal sealed class GatedHarnessFactory : IHarnessSessionFactory
{
    private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Releases every blocked stream.</summary>
    public void Release() => _ = _gate.TrySetResult(true);

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return new GatedSession(_gate);
    }

    /// <summary>A harness session that waits on the factory gate before completing.</summary>
    private sealed class GatedSession : IOmpSession
    {
        private readonly TaskCompletionSource<bool> _gate;

        /// <summary>Initializes a new instance of the <see cref="GatedSession"/> class.</summary>
        /// <param name="gate">The release signal.</param>
        public GatedSession(TaskCompletionSource<bool> gate)
        {
            _gate = gate;
        }

        /// <inheritdoc />
        public HarnessState State => HarnessState.Ready;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <inheritdoc />
        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
            string prompt,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            ArgumentNullException.ThrowIfNull(options);
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            yield return new CompletedEvent("end_turn");
        }

        /// <inheritdoc />
        public Task CancelAsync(CancellationToken cancellationToken)
        {
            _ = _gate.TrySetResult(true);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
