using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts;

/// <summary>
/// Orchestrates one sign-in: it asks for the email, keeps the per-address PKCE verifier and nonce across resends, enforces the resend cooldown and the
/// five-wrong-codes rule, checks a typed code or a link callback, and hands the resulting session to <see cref="AccountSessionManager"/>. Expected
/// failures come back as an <see cref="AuthFailure"/> with one <see cref="AuthErrorKind"/>; nothing here retries a request.
/// </summary>
public sealed class SignInCoordinator : IDisposable
{
    /// <summary>How many wrong codes are accepted before a new email must be requested.</summary>
    public const int MaxWrongCodes = 5;

    private const int MaxEmailLength = 254;

    private readonly AccountSessionManager _session;
    private readonly Func<CancellationToken, ValueTask<GoTrueClient?>> _clientProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SignInCoordinator>? _logger;
    private readonly Lock _gate = new();
    private Attempt? _attempt;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private int _activeSends;
    private bool _disposed;

    /// <summary>Creates a coordinator.</summary>
    /// <param name="session">The manager that receives the session once a sign-in succeeds.</param>
    /// <param name="clientProvider">Returns the client, or null while no project is configured (which fails with <see cref="AuthErrorKind.ConfigUnavailable"/>).</param>
    /// <param name="timeProvider">The clock for the cooldown and the code expiry.</param>
    /// <param name="logger">An optional logger. It never receives an email address, code, token, verifier or query.</param>
    public SignInCoordinator(
        AccountSessionManager session,
        Func<CancellationToken, ValueTask<GoTrueClient?>> clientProvider,
        TimeProvider timeProvider,
        ILogger<SignInCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(clientProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _session = session;
        _clientProvider = clientProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Gets the challenge of the attempt in progress, or null when none is.</summary>
    public SignInChallenge? Current
    {
        get
        {
            lock (_gate)
            {
                return _attempt?.ToChallenge();
            }
        }
    }

    /// <summary>Gets how many more wrong codes the attempt in progress accepts before a new email must be requested; zero when none is in progress.</summary>
    public int RemainingAttempts
    {
        get
        {
            lock (_gate)
            {
                return _attempt is null ? 0 : Math.Max(0, MaxWrongCodes - _attempt.WrongCodes);
            }
        }
    }

    /// <summary>
    /// Requests the email for an address. Asking again for the same address is a resend: it keeps the verifier and the nonce and obeys the cooldown.
    /// A different address starts a new attempt with a new verifier and nonce.
    /// </summary>
    /// <param name="email">The address the person typed.</param>
    /// <param name="linkRedirect">
    /// Called with a fresh nonce when the project offers the magic link, and returns the loopback address the link returns to; null (or a null answer, when
    /// the listener could not bind) means the code only. It is not called when the project does not offer the link.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The challenge to show, or why no email was requested. A resend inside the cooldown is <see cref="AuthErrorKind.TooManyRequests"/> with the remaining wait.</returns>
    public async Task<AuthResult<SignInChallenge>> SendCodeAsync(string email, Func<string, Uri?>? linkRedirect, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        string? address = NormalizeEmail(email);
        if (address is null)
        {
            return AuthResult.Fail<SignInChallenge>(AuthErrorKind.EmailInvalid);
        }

        GoTrueClient? client = await _clientProvider(cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return AuthResult.Fail<SignInChallenge>(AuthErrorKind.ConfigUnavailable);
        }

        if (!client.Options.EmailCodeEnabled)
        {
            return AuthResult.Fail<SignInChallenge>(AuthErrorKind.MethodUnavailable);
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _activeSends++;
        }

        try
        {
            await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await SendCodeCoreAsync(client, address, linkRedirect, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }
        }
        finally
        {
            lock (_gate)
            {
                _activeSends--;
                if (_disposed && _activeSends == 0)
                {
                    _sendGate.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Starts a sign-in with a third-party provider. The attempt keeps its verifier in memory, the same as a magic link, so <see cref="CompleteLinkAsync"/>
    /// exchanges the code the provider returns in the tab that started it. The caller opens the returned address.
    /// </summary>
    /// <param name="provider">The provider name the service registers: <c>google</c> or <c>x</c>.</param>
    /// <param name="linkRedirect">The return address for the given nonce. Null when none exists, and then the sign-in cannot start.</param>
    /// <param name="cancellationToken">Cancels the client lookup.</param>
    /// <returns>The authorize address, or why the provider sign-in cannot start.</returns>
    public async Task<AuthResult<Uri>> BeginProviderAsync(string provider, Func<string, Uri?> linkRedirect, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentNullException.ThrowIfNull(linkRedirect);
        GoTrueClient? client = await _clientProvider(cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return AuthResult.Fail<Uri>(AuthErrorKind.ConfigUnavailable);
        }

        string nonce = Pkce.CreateNonce();
        Uri? redirect = linkRedirect(nonce);
        if (redirect is null)
        {
            return AuthResult.Fail<Uri>(AuthErrorKind.ConfigUnavailable);
        }

        SecretString verifier = Pkce.CreateVerifier();
        Uri authorize;
        try
        {
            authorize = client.BuildProviderAuthorizeUri(provider, redirect, Pkce.ComputeChallenge(verifier));
        }
        catch (ArgumentException)
        {
            return AuthResult.Fail<Uri>(AuthErrorKind.ConfigUnavailable);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            _attempt = new Attempt(string.Empty, verifier, nonce, redirect)
            {
                SentAt = now,
                ExpiresAt = now + client.Options.EmailCodeExpiry,
                ResendAvailableAt = now + client.Options.ResendCooldown,
            };
        }

        return AuthResult.Success(authorize);
    }

    /// <summary>Requests another email for the attempt in progress, reusing its verifier and nonce.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new challenge, or why no email was requested.</returns>
    public Task<AuthResult<SignInChallenge>> ResendCodeAsync(CancellationToken cancellationToken)
    {
        string? email;
        lock (_gate)
        {
            email = _attempt?.Email;
        }

        return email is null
            ? Task.FromResult(AuthResult.Fail<SignInChallenge>(AuthErrorKind.Unexpected))
            : SendCodeAsync(email, linkRedirect: null, cancellationToken);
    }

    /// <summary>Checks the code the person typed. Spaces and hyphens are removed; the rest must be 6 to 10 digits.</summary>
    /// <param name="code">The code as typed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// Success once the session is current in the manager, otherwise why not. A wrong or expired code is <see cref="AuthErrorKind.CodeInvalidOrExpired"/> and counts
    /// toward <see cref="MaxWrongCodes"/>; once they are used up, or the code has expired by the clock, no request is made until a new email was requested.
    /// </returns>
    public async Task<AuthResult<AuthUnit>> VerifyCodeAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        Attempt? attempt;
        lock (_gate)
        {
            attempt = _attempt;
        }

        if (attempt is null)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.Unexpected);
        }

        if (GoTrueClient.NormalizeCode(code) is null
            || attempt.WrongCodes >= MaxWrongCodes
            || _timeProvider.GetUtcNow() >= attempt.ExpiresAt)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.CodeInvalidOrExpired);
        }

        GoTrueClient? client = await _clientProvider(cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.ConfigUnavailable);
        }

        AuthResult<GoTrueSession> verified = await client.VerifyEmailOtpAsync(attempt.Email, code, cancellationToken).ConfigureAwait(false);
        if (!verified.IsSuccess)
        {
            AuthFailure failure = verified.Failure;
            if (failure.Kind == AuthErrorKind.CodeInvalidOrExpired)
            {
                int wrong = CountWrongCode(attempt);
                AccountLogMessages.CodeRejected(_logger, wrong);
            }

            AccountLogMessages.SignInFailed(_logger, "code", failure.Kind);
            return AuthResult.Fail<AuthUnit>(failure);
        }

        await _session.SignInAsync(verified.Value, client.Options.Issuer, CancellationToken.None).ConfigureAwait(false);
        Reset(log: false);
        AccountLogMessages.SignInCompleted(_logger, "code");
        return AuthResult.Success(AuthUnit.Value);
    }

    /// <summary>Completes the magic link flow with the code a loopback callback carried.</summary>
    /// <param name="authCode">The code from the callback query. It is never logged.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// Success once the session is current in the manager, otherwise why not. A failed exchange keeps the attempt, so the emailed code still works.
    /// </returns>
    public async Task<AuthResult<AuthUnit>> CompleteLinkAsync(string authCode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authCode);
        Attempt? attempt;
        lock (_gate)
        {
            attempt = _attempt;
        }

        if (attempt?.Verifier is null)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.LinkInvalidOrExpired);
        }

        GoTrueClient? client = await _clientProvider(cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.ConfigUnavailable);
        }

        AuthResult<GoTrueSession> exchanged = await client.ExchangeCodeAsync(new SecretString(authCode), attempt.Verifier, cancellationToken).ConfigureAwait(false);
        if (!exchanged.IsSuccess)
        {
            AccountLogMessages.SignInFailed(_logger, "link", exchanged.Failure.Kind);
            return AuthResult.Fail<AuthUnit>(exchanged.Failure);
        }

        await _session.SignInAsync(exchanged.Value, client.Options.Issuer, CancellationToken.None).ConfigureAwait(false);
        Reset(log: false);
        AccountLogMessages.SignInCompleted(_logger, "link");
        return AuthResult.Success(AuthUnit.Value);
    }

    /// <summary>Forgets the attempt in progress: "Change email" and Cancel. The verifier and nonce are dropped; the address is the head's to keep.</summary>
    public void Reset() => Reset(log: true);

    /// <summary>Releases the send gate after any in-flight sends have finished.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_activeSends == 0)
            {
                _sendGate.Dispose();
            }
        }
    }

    private async Task<AuthResult<SignInChallenge>> SendCodeCoreAsync(
        GoTrueClient client,
        string address,
        Func<string, Uri?>? linkRedirect,
        CancellationToken cancellationToken)
    {
        Attempt? current;
        lock (_gate)
        {
            current = _attempt;
        }

        bool resend = current is not null && string.Equals(current.Email, address, StringComparison.OrdinalIgnoreCase);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (resend && now < current!.ResendAvailableAt)
        {
            AccountLogMessages.Cooldown(_logger);
            return AuthResult.Fail<SignInChallenge>(new AuthFailure(AuthErrorKind.TooManyRequests, current.ResendAvailableAt - now));
        }

        Attempt next = resend ? current! : BeginAttempt(address, client.Options, linkRedirect);
        string? challenge = next.Verifier is null ? null : Pkce.ComputeChallenge(next.Verifier);
        AuthResult<AuthUnit> sent = await client.SendEmailOtpAsync(address, challenge, next.RedirectTo, cancellationToken).ConfigureAwait(false);
        if (!sent.IsSuccess)
        {
            AccountLogMessages.CodeRequestRefused(_logger, sent.Failure.Kind);
            return AuthResult.Fail<SignInChallenge>(sent.Failure);
        }

        DateTimeOffset sentAt = _timeProvider.GetUtcNow();
        next = next with
        {
            SentAt = sentAt,
            ExpiresAt = sentAt + client.Options.EmailCodeExpiry,
            ResendAvailableAt = sentAt + client.Options.ResendCooldown,
            WrongCodes = 0,
            ShowFirstTimeHint = !client.Options.SignupEmailHasCode,
        };
        lock (_gate)
        {
            _attempt = next;
        }

        AccountLogMessages.CodeRequested(_logger, resend, next.Verifier is not null);
        return AuthResult.Success(next.ToChallenge());
    }

    private static string? NormalizeEmail(string email)
    {
        string trimmed = email.Trim();
        if (trimmed.Length is 0 or > MaxEmailLength)
        {
            return null;
        }

        int at = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
        {
            return null;
        }

        foreach (char c in trimmed)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return null;
            }
        }

        return trimmed;
    }

    private static Attempt BeginAttempt(string address, GoTrueOptions options, Func<string, Uri?>? linkRedirect)
    {
        if (!options.MagicLinkEnabled || linkRedirect is null)
        {
            return new Attempt(address, null, null, null);
        }

        string nonce = Pkce.CreateNonce();
        Uri? redirect = linkRedirect(nonce);
        return redirect is null ? new Attempt(address, null, null, null) : new Attempt(address, Pkce.CreateVerifier(), nonce, redirect);
    }

    private int CountWrongCode(Attempt seen)
    {
        lock (_gate)
        {
            if (_attempt is null || !ReferenceEquals(_attempt.Verifier, seen.Verifier) || !string.Equals(_attempt.Email, seen.Email, StringComparison.Ordinal))
            {
                return seen.WrongCodes + 1;
            }

            _attempt = _attempt with { WrongCodes = _attempt.WrongCodes + 1 };
            return _attempt.WrongCodes;
        }
    }

    private void Reset(bool log)
    {
        lock (_gate)
        {
            _attempt = null;
        }

        if (log)
        {
            AccountLogMessages.AttemptReset(_logger);
        }
    }

    private sealed record Attempt(string Email, SecretString? Verifier, string? Nonce, Uri? RedirectTo)
    {
        public DateTimeOffset SentAt { get; init; }

        public DateTimeOffset ExpiresAt { get; init; }

        public DateTimeOffset ResendAvailableAt { get; init; }

        public int WrongCodes { get; init; }

        public bool ShowFirstTimeHint { get; init; }

        public SignInChallenge ToChallenge() => new(Email, SentAt, ExpiresAt, ResendAvailableAt, Verifier is not null, Nonce, ShowFirstTimeHint);
    }
}
