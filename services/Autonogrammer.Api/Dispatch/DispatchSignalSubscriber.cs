using Autonogrammer.Coordination;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Subscribes to the coordination signals while the host runs. A signal for a device reaches the hub through the service provider, so the hub, and
/// with it the credential issuer and its keys, is built only when a signal arrives.
/// </summary>
internal sealed class DispatchSignalSubscriber : IHostedService
{
    private readonly IDispatchSignals _signals;
    private readonly IServiceProvider _services;
    private IAsyncDisposable? _subscription;

    /// <summary>Initializes a new instance of the <see cref="DispatchSignalSubscriber"/> class.</summary>
    /// <param name="signals">The coordination signals.</param>
    /// <param name="services">The service provider the hub is resolved from on a signal.</param>
    public DispatchSignalSubscriber(IDispatchSignals signals, IServiceProvider services)
    {
        _signals = signals;
        _services = services;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = await _signals.SubscribeAsync((deviceId, token) => new ValueTask(_services.GetRequiredService<DispatchHub>().OnSignalAsync(deviceId, token)), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscription is not null)
        {
            await _subscription.DisposeAsync().ConfigureAwait(false);
            _subscription = null;
        }
    }
}
