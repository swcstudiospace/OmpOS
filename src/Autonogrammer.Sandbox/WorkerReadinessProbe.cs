using System.Globalization;
using System.Runtime.InteropServices;

namespace Autonogrammer.Sandbox;

/// <summary>
/// Probes a worker's anonymous health path: one GET for a health check, or a retry loop that waits for a slept worker to wake.
/// </summary>
/// <remarks>
/// A 2xx answer means ready. 502, 503, 504, a connection failure and a per-request timeout are the signs of a worker that is
/// still waking, so the loop retries them every retry interval until the wake window, measured with the injected
/// <see cref="TimeProvider"/>, ends. Any other status (401, 403, 404, 500, ...) fails at once.
/// </remarks>
internal sealed class WorkerReadinessProbe
{
    private readonly HttpClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _wakeTimeout;
    private readonly TimeSpan _retryInterval;
    private readonly string _healthPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerReadinessProbe"/> class.
    /// </summary>
    /// <param name="client">The HTTP client used for probes; owned by the caller.</param>
    /// <param name="timeProvider">The clock for the wake window and the retry delay.</param>
    /// <param name="wakeTimeout">How long to retry; must be positive.</param>
    /// <param name="retryInterval">The pause between probes; must be positive.</param>
    /// <param name="healthPath">The health path relative to the worker base URI.</param>
    public WorkerReadinessProbe(HttpClient client, TimeProvider timeProvider, TimeSpan wakeTimeout, TimeSpan retryInterval, string healthPath)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(wakeTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryInterval, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrWhiteSpace(healthPath);
        _client = client;
        _timeProvider = timeProvider;
        _wakeTimeout = wakeTimeout;
        _retryInterval = retryInterval;
        _healthPath = healthPath;
    }

    /// <summary>
    /// Sends one health request.
    /// </summary>
    /// <param name="workerBaseUri">The worker base URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the worker answered 2xx; <see langword="false"/> for any other answer or a transport failure.</returns>
    public async Task<bool> ProbeOnceAsync(Uri workerBaseUri, CancellationToken cancellationToken)
    {
        var attempt = await AttemptAsync(workerBaseUri, cancellationToken).ConfigureAwait(false);
        return attempt.Outcome == AttemptOutcome.Ready;
    }

    /// <summary>
    /// Probes until the worker answers 2xx, retrying the signs of a waking worker for the wake window.
    /// </summary>
    /// <param name="workerBaseUri">The worker base URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the worker is ready.</returns>
    /// <exception cref="CloudEnvironmentException">
    /// <see cref="CloudEnvironmentFailure.WakeTimeout"/> when the window ends, or <see cref="CloudEnvironmentFailure.Transport"/>
    /// when the worker answers with a status that is not retried.
    /// </exception>
    public async Task WaitUntilReadyAsync(Uri workerBaseUri, CancellationToken cancellationToken)
    {
        var started = _timeProvider.GetUtcNow();
        while (true)
        {
            var attempt = await AttemptAsync(workerBaseUri, cancellationToken).ConfigureAwait(false);
            switch (attempt.Outcome)
            {
                case AttemptOutcome.Ready:
                    return;
                case AttemptOutcome.Fatal:
                    throw new CloudEnvironmentException(
                        string.Create(CultureInfo.InvariantCulture, $"The worker health probe answered HTTP {attempt.Status}."),
                        CloudEnvironmentFailure.Transport)
                    {
                        Operation = "workerProbe",
                    };
                case AttemptOutcome.Retry:
                default:
                    break;
            }

            if (_timeProvider.GetUtcNow() - started >= _wakeTimeout)
            {
                throw new CloudEnvironmentException(
                    string.Create(CultureInfo.InvariantCulture, $"The worker did not become ready within {_wakeTimeout.TotalSeconds:0.###} seconds."),
                    CloudEnvironmentFailure.WakeTimeout)
                {
                    Operation = "workerProbe",
                };
            }

            await Task.Delay(_retryInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Attempt> AttemptAsync(Uri workerBaseUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workerBaseUri);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(workerBaseUri, _healthPath));
        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            return status switch
            {
                >= 200 and < 300 => new Attempt(AttemptOutcome.Ready, status),
                502 or 503 or 504 => new Attempt(AttemptOutcome.Retry, status),
                _ => new Attempt(AttemptOutcome.Fatal, status),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Attempt(AttemptOutcome.Retry, 0);
        }
        catch (HttpRequestException)
        {
            return new Attempt(AttemptOutcome.Retry, 0);
        }
    }

    private enum AttemptOutcome
    {
        Ready,
        Retry,
        Fatal,
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Attempt(AttemptOutcome Outcome, int Status);
}
