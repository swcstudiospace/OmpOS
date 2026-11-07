using Autonogrammer.Contracts.Accounts;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts.Desktop;

/// <summary>
/// The desktop sign-in by emailed link with the code as fallback: it asks the <see cref="SignInCoordinator"/> to send the email with a PKCE challenge whose
/// <c>redirect_to</c> is a <see cref="LoopbackListener"/> address, completes the sign-in when the callback arrives, and leaves the code path of the
/// coordinator untouched when it does not. The flow never opens a browser.
/// </summary>
/// <remarks>
/// <para>
/// The verifier and the nonce belong to the coordinator's attempt: <see cref="ResendCodeAsync"/> reuses both (and so the running listener), a new
/// <see cref="SendCodeAsync"/> starts a fresh attempt and a fresh listener, and the verifier never leaves memory. No log line carries query text.
/// </para>
/// <para>
/// A link opened on another device cannot reach this loopback address; the panel says so with <see cref="LinkOnAnotherDeviceSentence"/>.
/// </para>
/// </remarks>
public sealed class PkceSignInFlow : IAsyncDisposable
{
    /// <summary>The one sentence a panel shows beside the code field: the limit of the loopback link (D-09).</summary>
    public const string LinkOnAnotherDeviceSentence = "A link opened on another device cannot sign you in here; use the code from the email instead.";

    private readonly SignInCoordinator _coordinator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PkceSignInFlow>? _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private LoopbackListener? _listener;
    private Task<AuthResult<AuthUnit>?>? _link;
    private string? _listenerNonce;

    /// <summary>Initializes a new instance of the <see cref="PkceSignInFlow"/> class.</summary>
    /// <param name="coordinator">The coordinator that owns the attempt, the verifier and the nonce.</param>
    /// <param name="timeProvider">The clock of the listener's read timeout and ten minute life.</param>
    /// <param name="logger">An optional logger. It never receives an email address, code, verifier, nonce or query.</param>
    public PkceSignInFlow(SignInCoordinator coordinator, TimeProvider timeProvider, ILogger<PkceSignInFlow>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _coordinator = coordinator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Gets the address the listener waits on, or null when no link is in play (the code is the way to sign in).</summary>
    public Uri? RedirectUri => _listener?.RedirectUri;

    /// <summary>
    /// Requests the email for an address. A different address starts a new attempt: a fresh verifier and nonce, and a fresh listener when the project
    /// offers the link and a loopback port could be bound (the previous attempt's listener is closed). The same address is a resend, as in the
    /// coordinator: it keeps the verifier, the nonce and the running listener.
    /// </summary>
    /// <param name="email">The address to sign in.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The challenge the panel shows, or why the email was not sent.</returns>
    public async Task<AuthResult<SignInChallenge>> SendCodeAsync(string email, CancellationToken cancellationToken)
    {
        LoopbackListener? started = null;
        string? startedNonce = null;
        AuthResult<SignInChallenge> result = await _coordinator.SendCodeAsync(
            email,
            nonce =>
            {
                started = LoopbackListener.TryStart(nonce, _timeProvider, _logger);
                startedNonce = nonce;
                return started?.RedirectUri;
            },
            cancellationToken).ConfigureAwait(false);

        if (started is not null)
        {
            await CloseListenerAsync().ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                await started.DisposeAsync().ConfigureAwait(false);
                return result;
            }

            _listener = started;
            _listenerNonce = startedNonce;
            _link = ObserveAsync(started, _lifetime.Token);
            return result;
        }

        if (result.IsSuccess && !string.Equals(result.Value.LinkNonce, _listenerNonce, StringComparison.Ordinal))
        {
            // A new attempt without a link: the old attempt's listener has no attempt left to serve.
            await CloseListenerAsync().ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Sends the code again. The verifier, the nonce and the running listener are reused.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The renewed challenge, or why the email was not sent.</returns>
    public Task<AuthResult<SignInChallenge>> ResendCodeAsync(CancellationToken cancellationToken) => _coordinator.ResendCodeAsync(cancellationToken);

    /// <summary>Checks a typed code. A successful sign-in closes the listener.</summary>
    /// <param name="code">The code as typed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Success, or why the code was not accepted.</returns>
    public async Task<AuthResult<AuthUnit>> VerifyCodeAsync(string code, CancellationToken cancellationToken)
    {
        AuthResult<AuthUnit> result = await _coordinator.VerifyCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await CloseListenerAsync().ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Waits for the link of the current attempt.</summary>
    /// <param name="cancellationToken">Abandons the wait; the link itself keeps waiting.</param>
    /// <returns>
    /// Null when no link is in play or the attempt was replaced or ended; success once the callback signed the person in; otherwise a failure. The failure
    /// <see cref="AuthErrorKind.LinkInvalidOrExpired"/> (the callback reported an error, or no callback arrived in ten minutes) means the code is the way
    /// to sign in; show <see cref="LinkOnAnotherDeviceSentence"/>.
    /// </returns>
    public Task<AuthResult<AuthUnit>?> WaitForLinkAsync(CancellationToken cancellationToken) =>
        _link is { } link ? link.WaitAsync(cancellationToken) : Task.FromResult<AuthResult<AuthUnit>?>(null);

    /// <summary>Ends the attempt: closes the listener and resets the coordinator.</summary>
    /// <returns>A task that completes once the listener is closed.</returns>
    public async Task ResetAsync()
    {
        await CloseListenerAsync().ConfigureAwait(false);
        _coordinator.Reset();
    }

    /// <summary>Closes the listener and ends the wait for a link.</summary>
    /// <returns>A task that completes once the listener is closed.</returns>
    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await CloseListenerAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }

    private async Task CloseListenerAsync()
    {
        LoopbackListener? listener = _listener;
        _listener = null;
        _link = null;
        _listenerNonce = null;
        if (listener is not null)
        {
            await listener.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<AuthResult<AuthUnit>?> ObserveAsync(LoopbackListener listener, CancellationToken cancellationToken)
    {
        LoopbackResult found;
        try
        {
            found = await listener.Result.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (found.Code is null)
        {
            DesktopLogMessages.LinkFallbackToCode(_logger);
            return AuthResult.Fail<AuthUnit>(AuthErrorKind.LinkInvalidOrExpired);
        }

        try
        {
            return await _coordinator.CompleteLinkAsync(found.Code.Reveal(), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
