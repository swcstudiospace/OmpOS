using System.Diagnostics.CodeAnalysis;

namespace Autonogrammer.Coordination;

/// <summary>
/// Process-local <see cref="IDispatchSignals"/>. Publishing invokes every subscriber registered at the moment of the call,
/// one after another. A subscriber that throws does not stop the others. Every subscriber runs, and then the failures are
/// raised together as one <see cref="AggregateException"/> from <see cref="PublishAsync"/>. The publisher therefore sees
/// handler failures, unlike the Dragonfly implementation, where delivery is asynchronous.
/// </summary>
public sealed class InMemoryDispatchSignals : IDispatchSignals
{
    private readonly Lock _gate = new();
    private readonly List<Subscription> _subscriptions = [];

    /// <summary>Delivers a wake-up for a device to every current subscriber.</summary>
    /// <param name="deviceId">The device that may have work waiting.</param>
    /// <param name="cancellationToken">Passed to each subscriber.</param>
    /// <exception cref="AggregateException">One or more subscribers threw. Every subscriber still received the signal.</exception>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A failing subscriber must not stop the others. The failures are collected and rethrown together after every subscriber ran.")]
    public async Task PublishAsync(string deviceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        cancellationToken.ThrowIfCancellationRequested();

        Subscription[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _subscriptions];
        }

        List<Exception>? failures = null;
        foreach (var subscription in snapshot)
        {
            try
            {
                await subscription.DeliverAsync(deviceId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures ??= new();
                failures.Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException("One or more dispatch signal subscribers failed.", failures);
        }
    }

    /// <summary>Subscribes a handler to wake-ups. Dispose the returned handle to unsubscribe.</summary>
    /// <param name="onSignal">Called with the device id of each signal.</param>
    /// <param name="cancellationToken">Checked once when subscribing. It does not end an established subscription.</param>
    /// <returns>A handle that unsubscribes when disposed.</returns>
    public Task<IAsyncDisposable> SubscribeAsync(Func<string, CancellationToken, ValueTask> onSignal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onSignal);
        cancellationToken.ThrowIfCancellationRequested();

        var subscription = new Subscription(this, onSignal);
        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }

        return Task.FromResult<IAsyncDisposable>(subscription);
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private sealed class Subscription : IAsyncDisposable
    {
        private readonly InMemoryDispatchSignals _owner;
        private readonly Func<string, CancellationToken, ValueTask> _onSignal;
        private int _disposed;

        public Subscription(InMemoryDispatchSignals owner, Func<string, CancellationToken, ValueTask> onSignal)
        {
            _owner = owner;
            _onSignal = onSignal;
        }

        public ValueTask DeliverAsync(string deviceId, CancellationToken cancellationToken)
        {
            return Volatile.Read(ref _disposed) != 0 ? ValueTask.CompletedTask : _onSignal(deviceId, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.Remove(this);
            }

            return ValueTask.CompletedTask;
        }
    }
}
