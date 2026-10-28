using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.EngineHost.Tests;

/// <summary>
/// A host that is disposed while its engine session is still opening (the child process is running, the handshake is not done)
/// stops that session at once. Without it the child would stay alive until the handshake ended, or for good when the agent never answers.
/// </summary>
public sealed class InFlightSessionDisposalTests
{
    private static readonly EngineHostDescriptor Descriptor = new() { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None };

    /// <summary>Disposing the host disposes the session whose open has not finished, and the prompt that waits for it ends with a failure.</summary>
    [Fact]
    public async Task DisposingTheHostStopsASessionThatIsStillOpening()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new OpeningSession();
        await using var sessionScope = session.ConfigureAwait(true);
        var host = new InProcessEngineHost(() => session, Descriptor);
        var turn = Task.Run(async () => await DrainAsync(host).ConfigureAwait(false), CancellationToken.None);
        await session.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(true);

        await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(true);

        session.Disposed.ShouldBeTrue();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => turn.WaitAsync(TimeSpan.FromSeconds(30), ct)).ConfigureAwait(true);
    }

    /// <summary>A session that finishes opening only after the host was disposed is disposed too, and the prompt is refused.</summary>
    [Fact]
    public async Task ASessionThatFinishesOpeningAfterTheHostWasDisposedIsDisposed()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new OpeningSession { FailWhenDisposed = false };
        await using var sessionScope = session.ConfigureAwait(true);
        var host = new InProcessEngineHost(() => session, Descriptor);
        var turn = Task.Run(async () => await DrainAsync(host).ConfigureAwait(false), CancellationToken.None);
        await session.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(true);

        await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(true);
        session.FinishOpening();

        _ = await Should.ThrowAsync<ObjectDisposedException>(() => turn.WaitAsync(TimeSpan.FromSeconds(30), ct)).ConfigureAwait(true);
        session.Disposed.ShouldBeTrue();
    }

    private static async Task DrainAsync(InProcessEngineHost host)
    {
        await foreach (var unused in host.SubmitPromptAsync("work", new PromptOptions(), CancellationToken.None).ConfigureAwait(false))
        {
            // Only the end of the turn matters.
        }
    }

    /// <summary>A session whose open waits until it is finished by the test or, when <see cref="FailWhenDisposed"/>, until it is disposed.</summary>
    private sealed class OpeningSession : IOmpSession
    {
        private readonly TaskCompletionSource _finish = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource OpenStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Disposed { get; private set; }

        public bool FailWhenDisposed { get; init; } = true;

        public HarnessState State { get; private set; } = HarnessState.Connecting;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public void FinishOpening() => _finish.TrySetResult();

        public async Task CreateAsync(CancellationToken cancellationToken)
        {
            OpenStarted.TrySetResult();
            await _finish.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (Disposed && FailWhenDisposed)
            {
                throw new InvalidOperationException("The session was disposed while it was opening.");
            }

            State = HarnessState.Ready;
        }

        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return new CompletedEvent("end_turn");
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            State = HarnessState.Disposed;
            if (FailWhenDisposed)
            {
                _finish.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }
    }
}
