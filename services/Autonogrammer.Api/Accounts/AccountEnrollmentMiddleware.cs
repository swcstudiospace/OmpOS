using System.Collections.Concurrent;
using Autonogrammer.Api.Middleware;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Accounts;

/// <summary>
/// Creates the caller's account row on the first authenticated OIDC request and then moves <c>last_seen_at</c> at most once per ten
/// minutes. Owner-token mode, the development principal and an unauthenticated request are skipped. A store failure is logged and never
/// blocks the request. The row holds no email.
/// </summary>
/// <remarks>
/// The write is bookkeeping, so it runs off the request: a request never waits for it, and a slow or locked store never stalls a route that does not read the row.
/// Pending writes are keyed by subject, so concurrent first requests of one caller share one write; at most <see cref="MaxPendingWrites"/> subjects have a
/// write in flight (a request beyond the bound skips enrollment and the next one retries). Each write
/// stops after <see cref="WriteTimeout"/> or when the host begins stopping. The one exception is an account route (<see cref="AccountRouteMarker"/>):
/// <c>GET /account</c> reads the row, so a request to an account route waits for its own pending write, bounded by <see cref="AccountRouteWait"/> and by the
/// request being aborted, and answers without the row when the write is slow or failed.
/// </remarks>
internal sealed class AccountEnrollmentMiddleware
{
    /// <summary>The most subjects with a write in flight; it also bounds the writes running at once.</summary>
    internal const int MaxPendingWrites = 64;

    /// <summary>How long one store write may run before it is abandoned.</summary>
    internal static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(10);

    /// <summary>How long an account route waits for its caller's write before it answers without the row.</summary>
    internal static readonly TimeSpan AccountRouteWait = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan WriteInterval = TimeSpan.FromMinutes(10);

    private readonly RequestDelegate _next;
    private readonly IAccountStore _store;
    private readonly TimeProvider _time;
    private readonly IOptionsMonitor<ApiAuthOptions> _auth;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<AccountEnrollmentMiddleware> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastWrite = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task> _pending = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="AccountEnrollmentMiddleware"/> class.</summary>
    /// <param name="next">The next pipeline step.</param>
    /// <param name="store">The account store.</param>
    /// <param name="time">The clock of the ten-minute cache and of the write timeouts.</param>
    /// <param name="auth">The authentication options.</param>
    /// <param name="lifetime">The host lifetime whose stopping token ends pending writes.</param>
    /// <param name="logger">The logger.</param>
    public AccountEnrollmentMiddleware(
        RequestDelegate next,
        IAccountStore store,
        TimeProvider time,
        IOptionsMonitor<ApiAuthOptions> auth,
        IHostApplicationLifetime lifetime,
        ILogger<AccountEnrollmentMiddleware> logger)
    {
        _next = next;
        _store = store;
        _time = time;
        _auth = auth;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>Starts the caller's enrollment when the request is an authenticated OIDC subject, then continues the pipeline.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the rest of the pipeline completes.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ShouldEnroll(context, out var subject))
        {
            if (TryEnroll(subject, out var pending) && context.GetEndpoint()?.Metadata.GetMetadata<AccountRouteMarker>() is not null)
            {
                await WaitBoundedAsync(pending, context.RequestAborted).ConfigureAwait(false);
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    private bool ShouldEnroll(HttpContext context, out string subject)
    {
        subject = string.Empty;
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (_auth.CurrentValue.Mode == ApiAuthMode.OwnerToken)
        {
            return false;
        }

        if (string.Equals(context.User.Identity.AuthenticationType, AuthBoundaryMiddleware.DevelopmentAuthenticationType, StringComparison.Ordinal))
        {
            return false;
        }

        var name = context.User.FindFirst(JwtBearerSetup.SubjectClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        subject = name;
        return true;
    }

    /// <summary>Starts, or joins, the caller's write. False when the ten-minute cache is fresh or the pending bound is reached.</summary>
    private bool TryEnroll(string subject, out Task pending)
    {
        pending = Task.CompletedTask;
        var now = _time.GetUtcNow();
        if (_lastWrite.TryGetValue(subject, out var last) && now - last < WriteInterval)
        {
            return false;
        }

        if (_pending.TryGetValue(subject, out var existing))
        {
            pending = existing;
            return true;
        }

        if (_pending.Count >= MaxPendingWrites)
        {
            return false;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(subject, completion.Task))
        {
            if (_pending.TryGetValue(subject, out var raced))
            {
                pending = raced;
                return true;
            }

            return false;
        }

        pending = completion.Task;
        _ = WriteAsync(subject, now, completion);
        return true;
    }

    /// <summary>Runs one write. It never throws; the completion source is set when the write ends, successful or not.</summary>
    private async Task WriteAsync(string subject, DateTimeOffset now, TaskCompletionSource completion)
    {
        var stopping = _lifetime.ApplicationStopping;
        try
        {
            using var timeout = new CancellationTokenSource(WriteTimeout, _time);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(stopping, timeout.Token);
            await _store.TouchAsync(subject, now, limit.Token).ConfigureAwait(false);
            _lastWrite[subject] = now;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stopping.IsCancellationRequested)
        {
            AccountLogMessages.StoreWriteFailed(_logger, ex);
        }
        catch (OperationCanceledException)
        {
            // The host is stopping: the write is abandoned and the next start enrolls the caller again.
        }
        finally
        {
            _ = _pending.TryRemove(subject, out _);
            _ = completion.TrySetResult();
        }
    }

    /// <summary>Waits for a write for at most <see cref="AccountRouteWait"/>; a slow write is left running and the request continues without the row.</summary>
    private async Task WaitBoundedAsync(Task pending, CancellationToken requestAborted)
    {
        try
        {
            await pending.WaitAsync(AccountRouteWait, _time, requestAborted).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The row is optional for the response: GET /account answers with null timestamps until the write lands.
        }
    }
}
