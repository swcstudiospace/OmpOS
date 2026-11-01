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
internal sealed class AccountEnrollmentMiddleware
{
    private static readonly TimeSpan WriteInterval = TimeSpan.FromMinutes(10);

    private readonly RequestDelegate _next;
    private readonly IAccountStore _store;
    private readonly TimeProvider _time;
    private readonly IOptionsMonitor<ApiAuthOptions> _auth;
    private readonly ILogger<AccountEnrollmentMiddleware> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastWrite = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="AccountEnrollmentMiddleware"/> class.</summary>
    /// <param name="next">The next pipeline step.</param>
    /// <param name="store">The account store.</param>
    /// <param name="time">The clock of the ten-minute cache.</param>
    /// <param name="auth">The authentication options.</param>
    /// <param name="logger">The logger.</param>
    public AccountEnrollmentMiddleware(
        RequestDelegate next,
        IAccountStore store,
        TimeProvider time,
        IOptionsMonitor<ApiAuthOptions> auth,
        ILogger<AccountEnrollmentMiddleware> logger)
    {
        _next = next;
        _store = store;
        _time = time;
        _auth = auth;
        _logger = logger;
    }

    /// <summary>Enrolls the caller when the request is an authenticated OIDC subject, then continues the pipeline.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the rest of the pipeline completes.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ShouldEnroll(context, out var subject))
        {
            await TouchAsync(subject).ConfigureAwait(false);
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

    private async Task TouchAsync(string subject)
    {
        var now = _time.GetUtcNow();
        if (_lastWrite.TryGetValue(subject, out var last) && now - last < WriteInterval)
        {
            return;
        }

        try
        {
            await _store.TouchAsync(subject, now, CancellationToken.None).ConfigureAwait(false);
            _lastWrite[subject] = now;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AccountLogMessages.StoreWriteFailed(_logger, ex);
        }
    }
}
