using StackExchange.Redis;

namespace Autonogrammer.Api.Coordination;

/// <summary>
/// Startup for the coordination backend. When the Dragonfly backend is registered, <see cref="StartingAsync"/> PINGs it before the web
/// server listens, so a host with an unreachable coordination store never serves a request. A failure stops startup with a message that
/// names <c>Coordination:Dragonfly:ConnectionString</c> and never includes its value; the underlying failure travels as the inner
/// exception. When the in-memory backend is registered it does nothing, except that in the Production environment it logs a warning,
/// which does not stop startup.
/// </summary>
/// <remarks>
/// Whether Dragonfly is in use is read from the registered <see cref="IConnectionMultiplexer"/>, which is the composition-time choice
/// (see <c>CoordinationRegistration</c>), so the PING and the warning always describe the backend that is actually running.
/// </remarks>
internal sealed class CoordinationStartup : IHostedLifecycleService
{
    private static readonly Action<ILogger, Exception?> InMemoryInProduction =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(32, "CoordinationInMemoryInProduction"),
            "Coordination:Store is InMemory in the Production environment. Run leases, idempotency records and session signals are not shared between instances; set Coordination:Store to Dragonfly.");

    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CoordinationStartup> _logger;

    /// <summary>Initializes a new instance of the <see cref="CoordinationStartup"/> class.</summary>
    /// <param name="services">The application service provider, used to find the Dragonfly multiplexer when it is registered.</param>
    /// <param name="environment">The host environment, used to decide whether the in-memory warning applies.</param>
    /// <param name="logger">The logger.</param>
    public CoordinationStartup(IServiceProvider services, IHostEnvironment environment, ILogger<CoordinationStartup> logger)
    {
        _services = services;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        var multiplexer = _services.GetService<IConnectionMultiplexer>();
        if (multiplexer is null)
        {
            if (_environment.IsProduction())
            {
                InMemoryInProduction(_logger, null);
            }

            return;
        }

        try
        {
            await multiplexer.GetDatabase().PingAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Startup must fail with one message that names the key; the cause is preserved as the inner exception.
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Coordination:Dragonfly:ConnectionString is set, but the Dragonfly PING failed at startup. The connection string value is not included.",
                ex);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
