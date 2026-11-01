using Autonogrammer.Accounts;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Presentation.Accounts;
using Autonogrammer.UI.Components.Account;
using Autonogrammer.UI.Components.Auth;
using Microsoft.JSInterop;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The OIDC account session of one browser tab. Tokens and the verifier live in an <see cref="InMemorySessionSecretStore"/>; a reload constructs a
/// new session and is signed out. A 429 waits <see cref="WebAuthOrigin.RateLimitCooldown"/> and never reads <c>Retry-After</c>.
/// </summary>
internal sealed class WebAccountSession : IApiTokenProvider, IAuthSession, IAccountHost, IDisposable
{
    private readonly TimeProvider _time;
    private readonly IServiceProvider _services;
    private readonly IJSRuntime? _js;
    private readonly Func<CancellationToken, Task<AccountResponse?>>? _readAccount;
    private readonly HttpMessageHandler? _authHandler;
    private readonly InMemorySessionSecretStore _store = new();
    private readonly AccountSessionManager _manager;
    private readonly SignInCoordinator _coordinator;
    private readonly DotNetObjectReference<WebAccountSession>? _self;
    private AccountHostState _state = AccountHostState.SignedOut;
    private GoTrueClient? _client;
    private HttpClient? _authHttp;
    private bool _signedIn;
    private string? _issuedToken;
    private bool _configured;
    private string? _webOrigin;

    /// <summary>Initializes a new instance of the <see cref="WebAccountSession"/> class.</summary>
    /// <param name="time">The clock of cooldowns.</param>
    /// <param name="services">The scope, used to read the account after sign-in without a construction cycle.</param>
    /// <param name="js">The runtime that binds the asking tab, or null in a test.</param>
    /// <param name="readAccount">A test double for <c>GET /account</c>, or null to use <see cref="IOmpApiClient"/>.</param>
    /// <param name="authHandler">The handler of the auth host, or null for the default handler. Tests own it.</param>
    internal WebAccountSession(
        TimeProvider time,
        IServiceProvider services,
        IJSRuntime? js = null,
        Func<CancellationToken, Task<AccountResponse?>>? readAccount = null,
        HttpMessageHandler? authHandler = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(services);
        _time = time;
        _services = services;
        _js = js;
        _readAccount = readAccount;
        _authHandler = authHandler;
        _manager = new AccountSessionManager(_store, _ => ValueTask.FromResult(_client), time);
        _coordinator = new SignInCoordinator(_manager, _ => ValueTask.FromResult(_client), time);
        _manager.StateChanged += OnManagerChanged;
        if (js is not null)
        {
            _self = DotNetObjectReference.Create(this);
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public AccountHostState State => _state;

    /// <inheritdoc />
    public bool HasToken => _signedIn;

    /// <inheritdoc />
    public bool SessionEnded { get; private set; }

    /// <inheritdoc />
    public string? ReturnPath { get; set; }

    /// <summary>Gets when a rate limit lifts, so a test can prove the 60 second cooldown.</summary>
    internal DateTimeOffset? RateLimitedUntil => _state.ResendAvailableAt;

    /// <summary>Applies an accepted OIDC configuration. A mismatched origin is not applied.</summary>
    /// <param name="config">The configuration.</param>
    /// <param name="builtOrigin">The built auth origin.</param>
    /// <param name="webOrigin">The page origin used as the link redirect.</param>
    internal void Apply(AuthConfig config, string? builtOrigin, string? webOrigin)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (WebAuthOrigin.Refuses(config, builtOrigin) || !config.HasClientBlock || config.ProjectUrl is null)
        {
            _configured = false;
            _client = null;
            Set(AccountHostState.SignedOut with { Configured = false, Phase = AuthPhase.Error, Error = AuthErrorKind.ConfigUnavailable });
            return;
        }

        var wire = new AuthConfigResponse(
            config.Mode,
            config.Authority,
            config.Audience,
            config.ProjectUrl,
            config.PublishableKey,
            config.Methods,
            config.EmailCode,
            config.SignupEmailHasCode);
        var options = GoTrueOptions.Create(wire);
        if (!options.IsValid || options.Options is null)
        {
            _configured = false;
            _client = null;
            Set(AccountHostState.SignedOut with { Configured = false, Phase = AuthPhase.Error, Error = AuthErrorKind.ConfigUnavailable });
            return;
        }

        _configured = true;
        _webOrigin = webOrigin;
        _authHttp?.Dispose();
        _authHttp = CreateAuthClient();
        _client = new GoTrueClient(_authHttp, options.Options, _time);
        _ = BindAskingTabAsync();
    }

    /// <inheritdoc />
    public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (!_signedIn)
        {
            return ValueTask.FromResult<string?>(null);
        }

        return ReadTokenAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (!_signedIn)
        {
            return;
        }

        EndSession();
        SessionEnded = false;
        Set(AccountHostState.SignedOut with { Configured = _configured });
        _ = _manager.SignOutAsync(SignOutScope.Local, CancellationToken.None);
    }

    /// <summary>
    /// Ends the account session for one refused request. It acts only when the refused token is the one last handed out, so a late 401 for an
    /// earlier token leaves a newer sign-in intact. The token is never logged.
    /// </summary>
    /// <param name="refusedToken">The token the refused request carried.</param>
    internal void Expire(string refusedToken)
    {
        if (!_signedIn || !string.Equals(_issuedToken, refusedToken, StringComparison.Ordinal))
        {
            return;
        }

        EndSession();
        SessionEnded = true;
        Set(AccountHostState.SignedOut with
        {
            Configured = _configured,
            Phase = AuthPhase.Error,
            Error = AuthErrorKind.AccountRejected,
        });
        _ = _manager.SignOutAsync(SignOutScope.Local, CancellationToken.None);
    }

    /// <summary>Receives a callback code from the asking-tab binding and exchanges it.</summary>
    /// <param name="code">The code. It is never logged.</param>
    /// <returns>A task that completes when the exchange has updated the state.</returns>
    [JSInvokable("ReceiveCode")]
    public Task ReceiveCodeAsync(string code) => ExchangeAsync(code);

    /// <inheritdoc />
    public async Task SendCodeAsync(string email, CancellationToken cancellationToken)
    {
        if (!_configured || _client is null)
        {
            Set(AccountHostState.SignedOut with { Configured = false, Phase = AuthPhase.Error, Error = AuthErrorKind.ConfigUnavailable });
            return;
        }

        if (_state.ResendAvailableAt is { } until && _time.GetUtcNow() < until && string.Equals(_state.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            Set(_state with { Phase = AuthPhase.Error, Error = AuthErrorKind.TooManyRequests });
            return;
        }

        Set(_state with { Phase = AuthPhase.SigningIn, Email = email, Error = null });
        var sent = await _coordinator.SendCodeAsync(email, LinkRedirect, cancellationToken).ConfigureAwait(false);
        if (!sent.IsSuccess)
        {
            ApplyFailure(sent.Failure, email);
            return;
        }

        var challenge = sent.Value;
        Set(AccountHostState.SignedOut with
        {
            Phase = AuthPhase.AwaitingLink,
            Email = challenge.Email,
            Configured = true,
            ExpiresAt = challenge.ExpiresAt,
            ResendAvailableAt = challenge.ResendAvailableAt,
            RemainingAttempts = _coordinator.RemainingAttempts,
            LoopbackUnavailable = !challenge.LinkAvailable && _client.Options.MagicLinkEnabled,
        });
    }

    /// <inheritdoc />
    public Task ResendCodeAsync(CancellationToken cancellationToken) =>
        SendCodeAsync(_state.Email ?? string.Empty, cancellationToken);

    /// <inheritdoc />
    public async Task SubmitCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (AuthCallbackHandOff.NormalizeEmailCode(code) is null)
        {
            Set(_state with { Phase = AuthPhase.Error, Error = AuthErrorKind.CodeInvalidOrExpired });
            return;
        }

        Set(_state with { Phase = AuthPhase.SigningIn });
        var verified = await _coordinator.VerifyCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (!verified.IsSuccess)
        {
            ApplyFailure(verified.Failure, _state.Email);
            return;
        }

        await AdoptSignedInAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _coordinator.Reset();
        Set(AccountHostState.SignedOut with { Configured = _configured, Email = _state.Email });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SignOutAsync(bool everywhere, CancellationToken cancellationToken)
    {
        EndSession();
        SessionEnded = false;
        Set(AccountHostState.SignedOut with { Configured = _configured });
        var scope = everywhere ? SignOutScope.Global : SignOutScope.Local;
        var result = await _manager.SignOutAsync(scope, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            Set(_state with { SignOutNotConfirmed = true });
        }
    }

    /// <inheritdoc />
    public Task RememberWithoutSystemProtectionAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void ChangeEmail()
    {
        _coordinator.Reset();
        Set(AccountHostState.SignedOut with { Configured = _configured });
    }

    /// <inheritdoc />
    public async Task RefreshDevicesAsync(CancellationToken cancellationToken)
    {
        if (!_signedIn)
        {
            Set(_state with { DevicesAvailable = false });
            return;
        }

        try
        {
            AccountResponse? account = _readAccount is not null
                ? await _readAccount(cancellationToken).ConfigureAwait(false)
                : await _services.GetRequiredService<IOmpApiClient>().GetAccountAsync(cancellationToken).ConfigureAwait(false);
            var open = account?.Features.Contains("dispatch", StringComparer.Ordinal) == true;
            Set(_state with { DevicesAvailable = open });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            Set(_state with { DevicesAvailable = false });
        }
        catch (InvalidOperationException)
        {
            Set(_state with { DevicesAvailable = false });
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _manager.StateChanged -= OnManagerChanged;
        _self?.Dispose();
        _manager.Dispose();
        _authHttp?.Dispose();
    }

    private Uri? LinkRedirect(string nonce)
    {
        _ = nonce;
        return string.IsNullOrEmpty(_webOrigin) ? null : new Uri(_webOrigin, UriKind.Absolute);
    }

    private async ValueTask<string?> ReadTokenAsync(CancellationToken cancellationToken)
    {
        var token = await _manager.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        _issuedToken = token.IsSuccess ? token.Value.Reveal() : null;
        return _issuedToken;
    }

    private async Task ExchangeAsync(string code)
    {
        if (_client is null)
        {
            return;
        }

        Set(_state with { Phase = AuthPhase.SigningIn });
        var exchanged = await _coordinator.CompleteLinkAsync(code, CancellationToken.None).ConfigureAwait(false);
        if (!exchanged.IsSuccess)
        {
            ApplyFailure(exchanged.Failure, _state.Email);
            return;
        }

        await AdoptSignedInAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task AdoptSignedInAsync(CancellationToken cancellationToken)
    {
        BeginSession();
        SessionEnded = false;
        var identity = _manager.State.Identity;
        Set(AccountHostState.SignedOut with
        {
            Phase = AuthPhase.SignedIn,
            Email = identity?.Email,
            Configured = true,
            Offline = _manager.State.Connectivity == AccountConnectivity.Offline,
        });
        await RefreshDevicesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ApplyFailure(AuthFailure failure, string? email)
    {
        var limited = failure.Kind is AuthErrorKind.TooManyRequests or AuthErrorKind.EmailRateLimited;
        var until = limited ? _time.GetUtcNow().Add(WebAuthOrigin.RateLimitCooldown) : _state.ResendAvailableAt;
        Set(_state with
        {
            Phase = AuthPhase.Error,
            Email = email,
            Error = failure.Kind,
            ResendAvailableAt = until,
            Configured = _configured,
        });
    }

    private void OnManagerChanged(object? sender, AccountStateChangedEventArgs e)
    {
        if (e.State.Phase == AuthPhase.SignedIn)
        {
            if (!_signedIn)
            {
                BeginSession();
            }
        }
    }

    /// <summary>
    /// Starts a signed-in session. The token last handed out belongs to an earlier session, so a refusal for it must not end this one.
    /// </summary>
    private void BeginSession()
    {
        _signedIn = true;
        _issuedToken = null;
    }

    /// <summary>Ends the signed-in session and forgets its token, so a late refusal for that token matches nothing.</summary>
    private void EndSession()
    {
        _signedIn = false;
        _issuedToken = null;
    }

    private void Set(AccountHostState state)
    {
        _state = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task BindAskingTabAsync()
    {
        if (_js is null || _self is null)
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("autonogrammerAuth.bindAskingTab", _self).ConfigureAwait(false);
        }
        catch (JSException)
        {
        }
    }

    private HttpClient CreateAuthClient()
    {
        if (_authHandler is not null)
        {
            return new HttpClient(_authHandler, disposeHandler: false);
        }

        HttpClientHandler? handler = null;
        try
        {
            // The browser fetch stack checks certificates. The revocation property is unsupported there (CA1416).
#pragma warning disable CA5399
            handler = new HttpClientHandler();
            var client = new HttpClient(handler, disposeHandler: true);
#pragma warning restore CA5399
            handler = null;
            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }
}
