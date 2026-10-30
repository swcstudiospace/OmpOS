using System.Diagnostics.Metrics;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.Telemetry;
using Autonogrammer.Harness.AgentLoop.Substrate;
using Autonogrammer.Resilience;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Audit;

/// <summary>
/// Drains <c>ledger_outbox</c> through the ledger pipeline (10 s, no pipeline retry, 2 in flight, breaker open 60 s). Attempt spacing is the outbox
/// schedule: 8 attempts, 5 s doubling to 10 min. A failure never reaches a user request. The loop stops when the host stopping token fires, inside the
/// 30 s shutdown timeout, because an in-flight emit is bound to that token.
/// </summary>
internal sealed class LedgerOutboxPump : IHostedService, ILedgerStatusSource, IDisposable
{
    private readonly IOptionsMonitor<DataPlaneOptions> _options;
    private readonly IOptions<LedgerResilienceOptions> _resilience;
    private readonly IAuditLedger _ledger;
    private readonly ILedgerOutboxStore _outbox;
    private readonly TimeProvider _time;
    private readonly ResiliencePipeline _pipeline;
    private readonly Meter _meter;
    private readonly System.Threading.Lock _lifetime = new();
    private CancellationTokenSource? _stopping;
    private Task? _loop;
    private int _disposeState;
    private int _cancelInFlight;
    private bool _resourcesDisposed;

    /// <summary>Initializes a new instance of the <see cref="LedgerOutboxPump"/> class.</summary>
    /// <param name="options">Whether the sink is enabled.</param>
    /// <param name="resilience">The outbox schedule and the pipeline policy.</param>
    /// <param name="ledger">The sink. The null sink is a no-op.</param>
    /// <param name="outbox">The outbox.</param>
    /// <param name="timeProvider">The clock for the schedule and the pipeline.</param>
    public LedgerOutboxPump(
        IOptionsMonitor<DataPlaneOptions> options,
        IOptions<LedgerResilienceOptions> resilience,
        IAuditLedger ledger,
        ILedgerOutboxStore outbox,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resilience);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _options = options;
        _resilience = resilience;
        _ledger = ledger;
        _outbox = outbox;
        _time = timeProvider;
        _pipeline = new ResiliencePipeline(resilience.Value.ToPolicy(IsLedgerFailure), timeProvider);
        _meter = new Meter(ApiTelemetry.SourceName);
        _ = _meter.CreateObservableGauge(
            "autonogrammer.ledger.outbox.depth",
            ObserveDepth,
            description: "Undelivered audit-ledger outbox rows. No tag carries an account, device or audit id.");
    }

    /// <inheritdoc />
    public DependencyState State => _options.CurrentValue.Ledger.Enabled ? _pipeline.State : DependencyState.Healthy;

    /// <inheritdoc />
    public long OutboxDepth => _outbox.Depth;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lifetime)
        {
            if (Volatile.Read(ref _disposeState) != 0 || _resourcesDisposed || _loop is not null)
            {
                return Task.CompletedTask;
            }

            _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loop = RunAsync(_stopping.Token);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => StopCoreAsync(cancellationToken);

    /// <summary>Sends every due row once. Tests call this instead of waiting on the loop.</summary>
    /// <param name="cancellationToken">Cancels the drain. An in-flight emit observes it.</param>
    /// <returns>A task that completes when this pass has finished.</returns>
    internal Task DrainOnceAsync(CancellationToken cancellationToken) => DrainAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        var state = Interlocked.CompareExchange(ref _disposeState, 1, 0);
        if (state == 2)
        {
            return;
        }

        if (state == 1)
        {
            var spin = new SpinWait();
            while (Volatile.Read(ref _disposeState) != 2)
            {
                spin.SpinOnce();
            }

            return;
        }

        try
        {
#pragma warning disable VSTHRD002 // Dispose is synchronous; the loop is observed before the token source is released.
            StopCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            ReleaseResources();
            Volatile.Write(ref _disposeState, 2);
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        Task? loop;
        CancellationTokenSource? stopping;
        lock (_lifetime)
        {
            loop = _loop;
            stopping = _resourcesDisposed ? null : _stopping;
            if (stopping is not null)
            {
                _cancelInFlight++;
            }
        }

        if (stopping is not null)
        {
            try
            {
                await stopping.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                lock (_lifetime)
                {
                    _cancelInFlight--;
                }
            }
        }

        if (loop is null)
        {
            return;
        }

        try
        {
            await loop.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ReleaseResources()
    {
        var spin = new SpinWait();
        while (true)
        {
            lock (_lifetime)
            {
                if (_resourcesDisposed)
                {
                    return;
                }

                if (Volatile.Read(ref _cancelInFlight) == 0)
                {
                    _resourcesDisposed = true;
                    try
                    {
                        _stopping?.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    _stopping = null;
                    _meter.Dispose();
                    return;
                }
            }

            spin.SpinOnce();
        }
    }

    /// <summary>The delay before the next try after <paramref name="attempts"/> failures. The first failure waits <c>FirstDelaySeconds</c>; each later failure doubles, capped at <c>MaxDelaySeconds</c>.</summary>
    /// <param name="attempts">The attempt count after the failure, starting at 1.</param>
    /// <param name="options">The schedule.</param>
    /// <returns>The delay.</returns>
    internal static TimeSpan DelayAfter(int attempts, LedgerResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var shift = Math.Clamp(attempts - 1, 0, 16);
        var seconds = options.FirstDelaySeconds * (1L << shift);
        if (seconds > options.MaxDelaySeconds)
        {
            seconds = options.MaxDelaySeconds;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), _time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await DrainAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        if (!_options.CurrentValue.Ledger.Enabled || _pipeline.State == DependencyState.Open)
        {
            return;
        }

        var due = await _outbox.ClaimDueAsync(_resilience.Value.InFlight, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        foreach (var item in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stored = await _pipeline.ExecuteAsync(
                    OperationClass.Background,
                    idempotent: true,
                    token => _ledger.NotariseAsync(item.Audit, token).AsTask(),
                    cancellationToken).ConfigureAwait(false);
                if (stored)
                {
                    await _outbox.MarkSentAsync(item.Id, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await ScheduleAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is SubstrateUnavailableException or DataPlaneTimeoutException or HttpRequestException or IOException)
            {
                await ScheduleAsync(item, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private ValueTask ScheduleAsync(LedgerOutboxItem item, CancellationToken cancellationToken)
    {
        var attempts = item.Attempts + 1;
        var next = attempts >= _resilience.Value.MaxAttempts
            ? DateTimeOffset.MaxValue
            : _time.GetUtcNow() + DelayAfter(attempts, _resilience.Value);
        return _outbox.ScheduleAsync(item.Id, attempts, next, cancellationToken);
    }

    private IEnumerable<Measurement<long>> ObserveDepth() => [new Measurement<long>(_outbox.Depth)];

    private static bool IsLedgerFailure(Exception exception) =>
        exception is SubstrateUnavailableException { IsReachabilityFailure: true }
        || ResiliencePolicy.DefaultIsTransient(exception);
}
