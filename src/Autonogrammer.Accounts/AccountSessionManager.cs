using System.Security.Cryptography;
using System.Text.Json;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts;

/// <summary>
/// The only holder of the signed-in session. It shares one refresh between all callers, writes the rotated refresh token to the secret store before the
/// new session is visible, starts a stored session at once without waiting on the network, treats being offline as a notice and never as a sign-out, and
/// clears local state before it tells the service about a sign-out.
/// </summary>
/// <remarks>
/// The manager takes the <see cref="GoTrueClient"/> through a deferred provider, so a head that learns the project URL and key from the API never
/// makes startup wait for them: startup reads only the stored session. Dispose the manager to stop its background work.
/// </remarks>
public sealed class AccountSessionManager : IAccountSession, IDisposable
{
    /// <summary>The name of the one secret that holds the stored session.</summary>
    public const string SecretName = "autonogrammer.account.session";

    /// <summary>The version of the stored shape.</summary>
    public const int StoredSchema = 1;

    private static readonly TimeSpan StartupRefreshBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SignOutBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LostResponseRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan OfflineRetryInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan OfflineRetryWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MinimumRefreshMargin = TimeSpan.FromSeconds(60);

    private static readonly Action<ILogger, Exception?> StoredSessionFound =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3010, "AccountStoredSessionFound"), "A stored session was found; it starts signed in and refreshes in the background.");

    private static readonly Action<ILogger, Exception?> StoredSessionUnreadable =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3011, "AccountStoredSessionUnreadable"), "The stored session could not be read and was discarded.");

    private static readonly Action<ILogger, string, string, Exception?> SecretStoreFailed =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(3012, "AccountSecretStoreFailed"), "The secret store failed during {Operation} with {ExceptionType}.");

    private static readonly Action<ILogger, Exception?> RefreshSucceeded =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3013, "AccountRefreshSucceeded"), "The session was refreshed.");

    private static readonly Action<ILogger, AuthErrorKind, string, Exception?> RefreshFailed =
        LoggerMessage.Define<AuthErrorKind, string>(LogLevel.Information, new EventId(3014, "AccountRefreshFailed"), "The session refresh failed: {Kind}, code {ServerCode}.");

    private static readonly Action<ILogger, Exception?> SessionEnded =
        LoggerMessage.Define(LogLevel.Information, new EventId(3015, "AccountSessionEnded"), "The session can no longer be refreshed and was cleared.");

    private static readonly Action<ILogger, Exception?> ServiceChanged =
        LoggerMessage.Define(LogLevel.Information, new EventId(3016, "AccountServiceChanged"), "The stored session belongs to another project and was discarded.");

    private static readonly Action<ILogger, Exception?> StoredTokenAdopted =
        LoggerMessage.Define(LogLevel.Information, new EventId(3017, "AccountStoredTokenAdopted"), "Another process rotated the refresh token; the stored one is used.");

    private static readonly Action<ILogger, Exception?> OfflineRetryScheduled =
        LoggerMessage.Define(LogLevel.Information, new EventId(3018, "AccountOfflineRetryScheduled"), "The service could not be reached; the session stays signed in and refreshes again later.");

    private static readonly Action<ILogger, SignOutScope, bool, Exception?> SignedOutLogged =
        LoggerMessage.Define<SignOutScope, bool>(LogLevel.Information, new EventId(3019, "AccountSignedOut"), "Signed out with scope {Scope}; confirmed by the service: {Confirmed}.");

    private static readonly Action<ILogger, Exception?> SignedInLogged =
        LoggerMessage.Define(LogLevel.Information, new EventId(3020, "AccountSignedIn"), "A session was established.");

    private static readonly Action<ILogger, string, Exception?> FlightFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3021, "AccountRefreshFlightFailed"), "The refresh stopped on an unexpected {ExceptionType}.");

    private readonly ISecretStore _store;
    private readonly Func<CancellationToken, ValueTask<GoTrueClient?>> _clientProvider;
    private readonly TimeProvider _time;
    private readonly IRefreshGuard _guard;
    private readonly ILogger<AccountSessionManager>? _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _mutation = new(1, 1);
    private readonly Lock _stateGate = new();
    private readonly Lock _flightGate = new();

    private AccountState _state = AccountState.SignedOut;
    private LiveSession? _live;
    private long _epoch;
    private int _started;
    private string? _knownStored;
    private Task<AuthResult<AuthUnit>>? _flight;
    private Task? _retryLoop;

    /// <summary>Creates a manager. Call <see cref="StartAsync"/> once to restore a stored session.</summary>
    /// <param name="store">Where the refresh token is kept between runs.</param>
    /// <param name="clientProvider">Returns the client, or null while no project is configured. It may be slow; nothing waits on it except the background refresh.</param>
    /// <param name="timeProvider">The clock for expiry, budgets and retry delays.</param>
    /// <param name="refreshGuard">Taken around every refresh; null means no cross-process guard.</param>
    /// <param name="logger">An optional logger. It never receives an email address, token or code.</param>
    public AccountSessionManager(
        ISecretStore store,
        Func<CancellationToken, ValueTask<GoTrueClient?>> clientProvider,
        TimeProvider timeProvider,
        IRefreshGuard? refreshGuard = null,
        ILogger<AccountSessionManager>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clientProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _clientProvider = clientProvider;
        _time = timeProvider;
        _guard = refreshGuard ?? NoRefreshGuard.Instance;
        _logger = logger;
        Jitter = DefaultJitter;
    }

    /// <inheritdoc />
    public event EventHandler<AccountStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public AccountState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    /// <summary>Gets or sets the function that spreads the offline retry interval. Tests replace it; the default is plus or minus 20 percent.</summary>
    internal Func<TimeSpan, TimeSpan> Jitter { get; set; }

    /// <summary>Gets the task of the refresh that startup began, or null when nothing was restored. Tests await it.</summary>
    internal Task<AuthResult<AuthUnit>>? StartupRefresh { get; private set; }

    /// <summary>Waits for the refresh that startup began. Tests call it; it completes with a failure when nothing was restored.</summary>
    /// <returns>The outcome of the startup refresh.</returns>
    internal Task<AuthResult<AuthUnit>> StartupRefreshAsync() =>
        StartupRefresh ?? Task.FromResult(AuthResult.Fail<AuthUnit>(AuthErrorKind.SessionEnded));

    /// <summary>Gets the access token callers would be handed right now, or null. Tests read it to prove the order of store write and publication.</summary>
    internal SecretString? CurrentAccessToken => Volatile.Read(ref _live)?.AccessToken;

    private enum Step
    {
        Done,
        Ended,
        Retry,
        Transient,
    }

    /// <summary>A manager provider that always answers one client, or null for none.</summary>
    /// <param name="client">The client, or null while no project is configured.</param>
    /// <returns>A provider for the constructor.</returns>
    public static Func<CancellationToken, ValueTask<GoTrueClient?>> FixedClient(GoTrueClient? client) => _ => ValueTask.FromResult(client);

    /// <summary>
    /// Restores a stored session without waiting on the network. A stored session makes the state <see cref="AuthPhase.SignedIn"/> at once with
    /// <see cref="AccountConnectivity.Refreshing"/>; one refresh whose every request has a 5 second budget (and, like any refresh, one retry after 1 second when
    /// the response was lost, which stays inside the service's reuse interval) then settles it to online or offline. Calling it again does nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read of the secret store.</param>
    /// <returns>A task that completes when the stored session has been read, not when the refresh has finished.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        (StoredSession? stored, bool unreadable) = await ReadStoredAsync(cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            if (unreadable)
            {
                if (_logger is not null)
                {
                    StoredSessionUnreadable(_logger, null);
                }

                _ = await TryRemoveStoredAsync(cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _knownStored = stored.RefreshToken;
            Volatile.Write(ref _live, new LiveSession(stored.Issuer, stored.UserId, stored.Email, new SecretString(stored.RefreshToken), null, DateTimeOffset.MinValue, TimeSpan.Zero));
            Publish(new AccountState(AuthPhase.SignedIn, new AccountIdentity(stored.UserId, stored.Email), AccountConnectivity.Refreshing, AccountNotices.None));
        }
        finally
        {
            _ = _mutation.Release();
        }

        if (_logger is not null)
        {
            StoredSessionFound(_logger, null);
        }

        StartupRefresh = RefreshSharedAsync(startup: true);
    }

    /// <summary>
    /// Makes a freshly issued session the current one: the refresh token is written to the secret store first, then the session is published. A store
    /// failure keeps the session in memory and sets <see cref="AccountNotices.SessionNotRemembered"/>.
    /// </summary>
    /// <param name="session">The session a sign-in produced.</param>
    /// <param name="issuer">The token issuer of the project that issued it, <see cref="GoTrueOptions.Issuer"/>.</param>
    /// <param name="cancellationToken">Cancels the wait for the manager.</param>
    /// <returns>A task that completes when the session is current.</returns>
    public async Task SignInAsync(GoTrueSession session, string issuer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ = Interlocked.Increment(ref _epoch);
            var live = new LiveSession(issuer, session.UserId, session.Email, session.RefreshToken, session.AccessToken, session.ExpiresAt, session.Lifetime);
            bool remembered = await TryWriteStoredAsync(live, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _live, live);
            Publish(new AccountState(
                AuthPhase.SignedIn,
                new AccountIdentity(live.UserId, live.Email),
                AccountConnectivity.Online,
                remembered ? AccountNotices.None : AccountNotices.SessionNotRemembered));
        }
        finally
        {
            _ = _mutation.Release();
        }

        if (_logger is not null)
        {
            SignedInLogged(_logger, null);
        }
    }

    /// <inheritdoc />
    public async ValueTask<AuthResult<SecretString>> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        LiveSession? live = Volatile.Read(ref _live);
        if (live is null)
        {
            return AuthResult.Fail<SecretString>(AuthErrorKind.SessionEnded);
        }

        if (IsFresh(live))
        {
            return AuthResult.Success(live.AccessToken!);
        }

        AuthResult<AuthUnit> refreshed = await RefreshSharedAsync(startup: false).WaitAsync(cancellationToken).ConfigureAwait(false);
        live = Volatile.Read(ref _live);
        if (live is null)
        {
            return AuthResult.Fail<SecretString>(AuthErrorKind.SessionEnded);
        }

        if (live.AccessToken is not null && (refreshed.IsSuccess || _time.GetUtcNow() < live.ExpiresAt))
        {
            return AuthResult.Success(live.AccessToken);
        }

        return AuthResult.Fail<SecretString>(refreshed.IsSuccess ? new AuthFailure(AuthErrorKind.Unexpected) : refreshed.Failure);
    }

    /// <inheritdoc />
    public async Task<AuthResult<AuthUnit>> SignOutAsync(SignOutScope scope, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown sign-out scope.");
        }

        if (scope == SignOutScope.Others)
        {
            return await SignOutOthersAsync(cancellationToken).ConfigureAwait(false);
        }

        LiveSession? live;
        bool cleared;
        await _mutation.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            live = Volatile.Read(ref _live);
            Volatile.Write(ref _live, null);
            _ = Interlocked.Increment(ref _epoch);
            cleared = await TryRemoveStoredAsync(CancellationToken.None).ConfigureAwait(false);
            Publish(AccountState.SignedOut);
        }
        finally
        {
            _ = _mutation.Release();
        }

        if (live is null)
        {
            if (!cleared)
            {
                Mutate(static state => state.Phase == AuthPhase.SignedOut ? state with { Notices = state.Notices | AccountNotices.SignOutNotCleared } : state);
            }

            return AuthResult.Success(AuthUnit.Value);
        }

        AuthResult<AuthUnit> revoked = await RevokeAsync(live, scope, cancellationToken).ConfigureAwait(false);
        if (!revoked.IsSuccess)
        {
            // A secret that could not be removed is only a risk while the service has not revoked its token: then the next start would restore the session.
            AccountNotices add = cleared ? AccountNotices.SignOutNotConfirmed : AccountNotices.SignOutNotConfirmed | AccountNotices.SignOutNotCleared;
            Mutate(state => state.Phase == AuthPhase.SignedOut ? state with { Notices = state.Notices | add } : state);
        }

        if (_logger is not null)
        {
            SignedOutLogged(_logger, scope, revoked.IsSuccess, null);
        }

        return revoked;
    }

    /// <summary>Stops the background work. A sign-out already under way finishes on its own budget.</summary>
    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _mutation.Dispose();
    }

    private static bool IsDefinitive(AuthFailure failure) =>
        failure.Kind is AuthErrorKind.SessionEnded or AuthErrorKind.AccountBlocked
        || failure.ServerCode is "refresh_token_not_found" or "refresh_token_already_used" or "session_not_found" or "session_expired" or "user_banned";

    private static TimeSpan DefaultJitter(TimeSpan interval)
    {
        double factor = 0.8 + (RandomNumberGenerator.GetInt32(0, 1001) / 1000.0 * 0.4);
        return interval * factor;
    }

    private bool IsFresh(LiveSession live)
    {
        if (live.AccessToken is null)
        {
            return false;
        }

        TimeSpan margin = TimeSpan.FromTicks(Math.Max(MinimumRefreshMargin.Ticks, live.Lifetime.Ticks / 5));
        return live.ExpiresAt - _time.GetUtcNow() > margin;
    }

    private Task<AuthResult<AuthUnit>> RefreshSharedAsync(bool startup)
    {
        TaskCompletionSource<AuthResult<AuthUnit>>? mine = null;
        Task<AuthResult<AuthUnit>> flight;
        lock (_flightGate)
        {
            if (_flight is null)
            {
                mine = new TaskCompletionSource<AuthResult<AuthUnit>>(TaskCreationOptions.RunContinuationsAsynchronously);
                _flight = mine.Task;
            }

            flight = _flight;
        }

        if (mine is not null)
        {
            _ = RunFlightAsync(startup, mine);
        }

        return flight;
    }

    private async Task RunFlightAsync(bool startup, TaskCompletionSource<AuthResult<AuthUnit>> completion)
    {
        AuthResult<AuthUnit> result;
        try
        {
            result = await RefreshCoreAsync(startup, _lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = AuthResult.Fail<AuthUnit>(AuthErrorKind.NetworkUnavailable);
            ScheduleRetryAfterFailure();
        }
#pragma warning disable CA1031 // The shared refresh is a boundary: every caller must get a result, never a thrown exception.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            if (_logger is not null)
            {
                FlightFailed(_logger, exception.GetType().Name, null);
            }

            result = AuthResult.Fail<AuthUnit>(AuthErrorKind.Unexpected);
            ScheduleRetryAfterFailure();
        }

        lock (_flightGate)
        {
            _flight = null;
        }

        completion.SetResult(result);
    }

    private async Task<AuthResult<AuthUnit>> RefreshCoreAsync(bool startup, CancellationToken cancellationToken)
    {
        long epoch = Interlocked.Read(ref _epoch);
        GoTrueClient? client = await _clientProvider(cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            SetConnectivity(AccountConnectivity.Offline);
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.ConfigUnavailable);
        }

        LiveSession? live = Volatile.Read(ref _live);
        if (live is null)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.SessionEnded);
        }

        if (!string.Equals(live.Issuer, client.Options.Issuer, StringComparison.Ordinal))
        {
            await DiscardAsync(epoch, AccountNotices.ServiceChanged, cancellationToken).ConfigureAwait(false);
            if (_logger is not null)
            {
                ServiceChanged(_logger, null);
            }

            return AuthResult.Fail<AuthUnit>(AuthErrorKind.SessionEnded);
        }

        bool lostResponseRetried = false;
        bool reread = true;
        while (true)
        {
            (Step step, AuthFailure? failure) = await AttemptAsync(client, startup, reread, epoch, cancellationToken).ConfigureAwait(false);
            switch (step)
            {
                case Step.Done:
                    return AuthResult.Success(AuthUnit.Value);
                case Step.Ended:
                    return AuthResult.Fail<AuthUnit>(failure ?? new AuthFailure(AuthErrorKind.SessionEnded));
                case Step.Retry:
                    reread = false;
                    break;
                default:
                    if (!lostResponseRetried)
                    {
                        lostResponseRetried = true;
                        await Task.Delay(LostResponseRetryDelay, _time, cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    SetConnectivity(AccountConnectivity.Offline);
                    EnsureOfflineRetryLoop();
                    return AuthResult.Fail<AuthUnit>(failure ?? new AuthFailure(AuthErrorKind.NetworkUnavailable));
            }
        }
    }

    private async Task<(Step Step, AuthFailure? Failure)> AttemptAsync(GoTrueClient client, bool startup, bool reread, long epoch, CancellationToken cancellationToken)
    {
        IAsyncDisposable handle = await _guard.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using (handle.ConfigureAwait(false))
        {
            return await AttemptHeldAsync(client, startup, reread, epoch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(Step Step, AuthFailure? Failure)> AttemptHeldAsync(GoTrueClient client, bool startup, bool reread, long epoch, CancellationToken cancellationToken)
    {
        LiveSession? live = Volatile.Read(ref _live);
        if (live is null || Interlocked.Read(ref _epoch) != epoch)
        {
            return (Step.Ended, new AuthFailure(AuthErrorKind.SessionEnded));
        }

        live = await AdoptStoredTokenAsync(epoch, live, cancellationToken).ConfigureAwait(false);
        if (live is null)
        {
            return (Step.Ended, new AuthFailure(AuthErrorKind.SessionEnded));
        }

        AuthResult<GoTrueSession> result = await RequestRefreshAsync(client, live.RefreshToken, startup ? StartupRefreshBudget : null, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            bool applied = await ApplyRefreshedAsync(epoch, live, result.Value, cancellationToken).ConfigureAwait(false);
            return applied ? (Step.Done, null) : (Step.Ended, new AuthFailure(AuthErrorKind.SessionEnded));
        }

        AuthFailure failure = result.Failure;
        if (_logger is not null)
        {
            RefreshFailed(_logger, failure.Kind, failure.ServerCode ?? "none", null);
        }

        if (!IsDefinitive(failure))
        {
            return (Step.Transient, failure);
        }

        if (reread)
        {
            StoredSession? again = (await ReadStoredAsync(cancellationToken).ConfigureAwait(false)).Stored;
            if (again is not null && IsRotatedElsewhere(again, live))
            {
                LiveSession? adopted = await AdoptAsync(epoch, live, again, cancellationToken).ConfigureAwait(false);
                return adopted is null ? (Step.Ended, new AuthFailure(AuthErrorKind.SessionEnded)) : (Step.Retry, null);
            }
        }

        await DiscardAsync(epoch, AccountNotices.SessionEnded, cancellationToken).ConfigureAwait(false);
        if (_logger is not null)
        {
            SessionEnded(_logger, null);
        }

        return (Step.Ended, failure);
    }

    private async Task<LiveSession?> AdoptStoredTokenAsync(long epoch, LiveSession live, CancellationToken cancellationToken)
    {
        StoredSession? stored = (await ReadStoredAsync(cancellationToken).ConfigureAwait(false)).Stored;
        return stored is null || !IsRotatedElsewhere(stored, live) ? live : await AdoptAsync(epoch, live, stored, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Makes the refresh token another process stored the live one, unless a sign-in, sign-out or discard has ended this refresh's session since it began.</summary>
    private async Task<LiveSession?> AdoptAsync(long epoch, LiveSession live, StoredSession stored, CancellationToken cancellationToken)
    {
        LiveSession adopted;
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _live) is null || Interlocked.Read(ref _epoch) != epoch)
            {
                return null;
            }

            adopted = live with { RefreshToken = new SecretString(stored.RefreshToken) };
            _knownStored = stored.RefreshToken;
            Volatile.Write(ref _live, adopted);
        }
        finally
        {
            _ = _mutation.Release();
        }

        if (_logger is not null)
        {
            StoredTokenAdopted(_logger, null);
        }

        return adopted;
    }

    private bool IsRotatedElsewhere(StoredSession stored, LiveSession live) =>
        string.Equals(stored.Issuer, live.Issuer, StringComparison.Ordinal)
        && string.Equals(stored.UserId, live.UserId, StringComparison.Ordinal)
        && !string.Equals(stored.RefreshToken, live.RefreshToken.Reveal(), StringComparison.Ordinal)
        && !string.Equals(stored.RefreshToken, _knownStored, StringComparison.Ordinal);

    private async Task<AuthResult<GoTrueSession>> RequestRefreshAsync(GoTrueClient client, SecretString refreshToken, TimeSpan? budget, CancellationToken cancellationToken)
    {
        using CancellationTokenSource? limit = budget is { } span ? new CancellationTokenSource(span, _time) : null;
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit?.Token ?? CancellationToken.None);
        try
        {
            return await client.RefreshAsync(refreshToken, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthResult.Fail<GoTrueSession>(AuthErrorKind.NetworkUnavailable);
        }
    }

    private async Task<bool> ApplyRefreshedAsync(long epoch, LiveSession previous, GoTrueSession session, CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _live) is null || Interlocked.Read(ref _epoch) != epoch)
            {
                return false;
            }

            var next = new LiveSession(
                previous.Issuer,
                session.UserId,
                session.Email ?? previous.Email,
                session.RefreshToken,
                session.AccessToken,
                session.ExpiresAt,
                session.Lifetime);

            bool remembered = await TryWriteStoredAsync(next, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _live, next);
            AccountNotices notices = State.Notices & ~AccountNotices.SessionNotRemembered;
            Publish(new AccountState(
                AuthPhase.SignedIn,
                new AccountIdentity(next.UserId, next.Email),
                AccountConnectivity.Online,
                remembered ? notices : notices | AccountNotices.SessionNotRemembered));
        }
        finally
        {
            _ = _mutation.Release();
        }

        if (_logger is not null)
        {
            RefreshSucceeded(_logger, null);
        }

        return true;
    }

    private async Task DiscardAsync(long epoch, AccountNotices notice, CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Interlocked.Read(ref _epoch) != epoch)
            {
                return;
            }

            Volatile.Write(ref _live, null);
            _ = Interlocked.Increment(ref _epoch);
            _ = await TryRemoveStoredAsync(cancellationToken).ConfigureAwait(false);
            Publish(AccountState.SignedOut with { Notices = notice });
        }
        finally
        {
            _ = _mutation.Release();
        }
    }

    private void EnsureOfflineRetryLoop()
    {
        lock (_flightGate)
        {
            if (_retryLoop is { IsCompleted: false })
            {
                return;
            }

            _retryLoop = OfflineRetryLoopAsync(_lifetime.Token);
        }

        if (_logger is not null)
        {
            OfflineRetryScheduled(_logger, null);
        }
    }

    private async Task OfflineRetryLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset deadline = _time.GetUtcNow() + OfflineRetryWindow;
            while (_time.GetUtcNow() < deadline)
            {
                await Task.Delay(Jitter(OfflineRetryInterval), _time, cancellationToken).ConfigureAwait(false);
                if (_live is null || State.Connectivity != AccountConnectivity.Offline)
                {
                    return;
                }

                AuthResult<AuthUnit> result = await RefreshSharedAsync(startup: false).ConfigureAwait(false);
                if (result.IsSuccess || _live is null)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The manager was disposed; the loop simply ends.
        }
    }

    private async Task<AuthResult<AuthUnit>> SignOutOthersAsync(CancellationToken cancellationToken)
    {
        AuthResult<SecretString> token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!token.IsSuccess)
        {
            return AuthResult.Fail<AuthUnit>(token.Failure);
        }

        GoTrueClient? client = await _clientProvider(cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.ConfigUnavailable);
        }

        using var budget = new CancellationTokenSource(SignOutBudget, _time);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.Token);
        try
        {
            return await client.SignOutAsync(token.Value, SignOutScope.Others, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.NetworkUnavailable);
        }
    }

    private async Task<AuthResult<AuthUnit>> RevokeAsync(LiveSession live, SignOutScope scope, CancellationToken cancellationToken)
    {
        GoTrueClient? client;
        try
        {
            client = await _clientProvider(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.NetworkUnavailable);
        }

        if (client is null)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.ConfigUnavailable);
        }

        using var budget = new CancellationTokenSource(SignOutBudget, _time);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.Token);
        try
        {
            SecretString? access = live.AccessToken is not null && _time.GetUtcNow() < live.ExpiresAt ? live.AccessToken : null;
            if (access is null)
            {
                AuthResult<GoTrueSession> refreshed = await client.RefreshAsync(live.RefreshToken, linked.Token).ConfigureAwait(false);
                if (!refreshed.IsSuccess)
                {
                    return AuthResult.Fail<AuthUnit>(refreshed.Failure);
                }

                access = refreshed.Value.AccessToken;
            }

            return await client.SignOutAsync(access, scope, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.NetworkUnavailable);
        }
    }

    private async Task<(StoredSession? Stored, bool Unreadable)> ReadStoredAsync(CancellationToken cancellationToken)
    {
        SecretString? secret;
        try
        {
            secret = await _store.GetAsync(SecretName, cancellationToken).ConfigureAwait(false);
        }
        catch (SecretStoreException exception)
        {
            LogStoreFailure("read", exception);
            return (null, false);
        }

        if (secret is null)
        {
            return (null, false);
        }

        try
        {
            StoredSession? stored = JsonSerializer.Deserialize(secret.Reveal(), StoredSessionJsonContext.Default.StoredSession);
            return stored is { Schema: StoredSchema }
                && !string.IsNullOrEmpty(stored.Issuer)
                && !string.IsNullOrEmpty(stored.RefreshToken)
                && !string.IsNullOrEmpty(stored.UserId)
                ? (stored, false)
                : (null, true);
        }
        catch (JsonException)
        {
            return (null, true);
        }
    }

    private async Task<bool> TryWriteStoredAsync(LiveSession live, CancellationToken cancellationToken)
    {
        var stored = new StoredSession(StoredSchema, live.Issuer, live.RefreshToken.Reveal(), live.UserId, live.Email);
        try
        {
            string json = JsonSerializer.Serialize(stored, StoredSessionJsonContext.Default.StoredSession);
            await _store.SetAsync(SecretName, new SecretString(json), cancellationToken).ConfigureAwait(false);
            _knownStored = stored.RefreshToken;
            return true;
        }
        catch (SecretStoreException exception)
        {
            LogStoreFailure("write", exception);
            return false;
        }
    }

    private async Task<bool> TryRemoveStoredAsync(CancellationToken cancellationToken)
    {
        // One immediate retry: a keyring that was busy or momentarily locked often answers the second request.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                _ = await _store.RemoveAsync(SecretName, cancellationToken).ConfigureAwait(false);
                _knownStored = null;
                return true;
            }
            catch (SecretStoreException exception)
            {
                LogStoreFailure("remove", exception);
            }
        }

        return false;
    }

    private void ScheduleRetryAfterFailure()
    {
        // A stopped manager has nothing to retry; a flight that fails otherwise must not leave the state at Refreshing with nobody to try again.
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }

        try
        {
            SetConnectivity(AccountConnectivity.Offline);
            EnsureOfflineRetryLoop();
        }
        catch (ObjectDisposedException)
        {
            // The manager was disposed between the check and the call.
        }
    }

    private void LogStoreFailure(string operation, Exception exception)
    {
        if (_logger is not null)
        {
            // Only the operation and the exception type are logged: the message is not trusted to be free of secrets.
            SecretStoreFailed(_logger, operation, exception.GetType().Name, null);
        }
    }

    private void SetConnectivity(AccountConnectivity connectivity) =>
        Mutate(state => state.Phase == AuthPhase.SignedIn ? state with { Connectivity = connectivity } : state);

    private void Mutate(Func<AccountState, AccountState> change)
    {
        AccountState next;
        lock (_stateGate)
        {
            next = change(_state);
            if (next.Equals(_state))
            {
                return;
            }

            _state = next;
        }

        StateChanged?.Invoke(this, new AccountStateChangedEventArgs(next));
    }

    private void Publish(AccountState next) => Mutate(_ => next);

    private sealed record LiveSession(
        string Issuer,
        string UserId,
        string? Email,
        SecretString RefreshToken,
        SecretString? AccessToken,
        DateTimeOffset ExpiresAt,
        TimeSpan Lifetime);
}
