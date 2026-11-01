using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Autonogrammer.Accounts;
using Autonogrammer.Accounts.Desktop;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Native.Settings;
using Autonogrammer.Presentation.Accounts;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// The desktop <see cref="IAccountHost"/>. It draws the session manager's state, runs the PKCE flow, reads <c>GET /account</c> for the Devices row, and
/// keeps the two account settings. A stored session is shown signed in before any network call. The GoTrue client is created only when a sign-in needs
/// it, from <c>GET /auth/config</c> on the same API client as the cloud host.
/// </summary>
internal sealed class DesktopAccountHost : IAccountHost, IAsyncDisposable
{
    private readonly IAccountSession _session;
    private readonly AccountSessionManager? _manager;
    private readonly SignInCoordinator? _coordinator;
    private readonly PkceSignInFlow? _flow;
    private readonly AccountSecretStoreSelector? _selector;
    private readonly FileSettingsStore _settings;
    private readonly HttpClient? _http;
    private readonly Uri? _apiAddress;
    private readonly TimeProvider _time;
    private readonly ILogger<GoTrueClient>? _clientLogger;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Lock _gate = new();
    private AccountHostState _state = AccountHostState.SignedOut;
    private GoTrueClient? _client;
    private int _clientResolved;
    private bool _configured = true;
    private CancellationTokenSource? _operation;

    /// <summary>Creates a host over pieces the head already built.</summary>
    /// <param name="session">The session the token supplier and the panel share.</param>
    /// <param name="manager">The manager, or null when a test injected the session.</param>
    /// <param name="coordinator">The coordinator, or null when a test injected the session.</param>
    /// <param name="flow">The PKCE flow, or null when a test injected the session.</param>
    /// <param name="selector">The secret-store selector, or null when a test injected the session.</param>
    /// <param name="settings">The head settings, which hold the two account keys.</param>
    /// <param name="http">The API client <see cref="HeadServices"/> built, or null when no API address is configured.</param>
    /// <param name="apiAddress">The API address, or null when none is configured.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="clientLogger">An optional GoTrue logger. It never receives a secret.</param>
    public DesktopAccountHost(
        IAccountSession session,
        AccountSessionManager? manager,
        SignInCoordinator? coordinator,
        PkceSignInFlow? flow,
        AccountSecretStoreSelector? selector,
        FileSettingsStore settings,
        HttpClient? http,
        Uri? apiAddress,
        TimeProvider timeProvider,
        ILogger<GoTrueClient>? clientLogger = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _session = session;
        _manager = manager;
        _coordinator = coordinator;
        _flow = flow;
        _selector = selector;
        _settings = settings;
        _http = http;
        _apiAddress = apiAddress;
        _time = timeProvider;
        _clientLogger = clientLogger;
        _configured = apiAddress is not null;
        _session.StateChanged += OnSessionChanged;
        if (_selector is not null)
        {
            _selector.Changed += OnSelectorChanged;
        }

        Publish(FromSession(_session.State), raise: false);
    }

    /// <summary>Occurs when <see cref="State"/> changed.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc />
    public AccountHostState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>Gets the selector, so a test can prove the next write after consent uses the file store.</summary>
    internal AccountSecretStoreSelector? Selector => _selector;

    /// <summary>Gets the session the token supplier reads.</summary>
    internal IAccountSession Session => _session;

    /// <summary>Restores a stored session when the setting says one exists. It does not wait for the network and does not ask the secret store otherwise.</summary>
    /// <param name="cancellationToken">Cancels the store read.</param>
    /// <returns>A task that completes when the panel can draw. A refresh, if any, continues in the background.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_manager is not null && _settings.ReadHasStoredSession())
        {
            await _manager.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        Publish(FromSession(_session.State), raise: true);
    }

    /// <inheritdoc />
    public async Task SendCodeAsync(string email, CancellationToken cancellationToken)
    {
        if (_flow is null)
        {
            Publish(State with { Phase = AuthPhase.Error, Error = AuthErrorKind.ConfigUnavailable }, raise: true);
            return;
        }

        using var linked = Link(cancellationToken);
        AuthResult<SignInChallenge> result = await _flow.SendCodeAsync(email, linked.Token).ConfigureAwait(false);
        if (linked.IsCancellationRequested && !result.IsSuccess)
        {
            Publish(State with { Phase = AuthPhase.SignedOut, Error = null }, raise: true);
            return;
        }

        ApplyChallenge(result);
    }

    /// <inheritdoc />
    public async Task ResendCodeAsync(CancellationToken cancellationToken)
    {
        if (_flow is null)
        {
            return;
        }

        using var linked = Link(cancellationToken);
        ApplyChallenge(await _flow.ResendCodeAsync(linked.Token).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task SubmitCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (_flow is null || _coordinator is null)
        {
            return;
        }

        using var linked = Link(cancellationToken);
        AuthResult<AuthUnit> result = await _flow.VerifyCodeAsync(code, linked.Token).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            bool mustResend = _coordinator.RemainingAttempts <= 0;
            var phase = mustResend ? AuthPhase.AwaitingLink : AuthPhase.Error;
            Publish(State with { Phase = phase, Error = mustResend ? null : result.Failure.Kind, MustResend = mustResend, RemainingAttempts = _coordinator.RemainingAttempts }, raise: true);
            return;
        }

        NoteSignedIn();
        Publish(FromSession(_session.State), raise: true);
    }

    /// <inheritdoc />
    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        if (_operation is not null)
        {
            await _operation.CancelAsync().ConfigureAwait(false);
        }
        if (_flow is not null)
        {
            await _flow.ResetAsync().ConfigureAwait(false);
        }

        _ = cancellationToken;
        Publish(State with { Phase = AuthPhase.SignedOut, Error = null, MustResend = false }, raise: true);
    }

    /// <inheritdoc />
    public async Task SignOutAsync(bool everywhere, CancellationToken cancellationToken)
    {
        var scope = everywhere ? SignOutScope.Global : SignOutScope.Local;
        AuthResult<AuthUnit> result = await _session.SignOutAsync(scope, cancellationToken).ConfigureAwait(false);
        _settings.SetHasStoredSession(false);
        var next = FromSession(_session.State);
        if (!result.IsSuccess)
        {
            next = next with { SignOutNotConfirmed = true };
        }

        Publish(next, raise: true);
    }

    /// <inheritdoc />
    public async Task RememberWithoutSystemProtectionAsync(CancellationToken cancellationToken)
    {
        if (_selector is null)
        {
            return;
        }

        await _selector.RememberWithoutSystemProtectionAsync(cancellationToken).ConfigureAwait(false);
        _settings.SetAllowFileSecretStore(true);
        PersistSelector();
    }

    /// <inheritdoc />
    public void ChangeEmail()
    {
        _ = _flow?.ResetAsync();
        Publish(AccountHostState.SignedOut with { Configured = _configured, SignOutNotConfirmed = State.SignOutNotConfirmed, ServiceChanged = State.ServiceChanged }, raise: true);
    }

    /// <inheritdoc />
    public async Task RefreshDevicesAsync(CancellationToken cancellationToken)
    {
        bool open = false;
        try
        {
            if (_http is null || _apiAddress is null || _session.State.Phase != AuthPhase.SignedIn)
            {
                return;
            }

            AuthResult<SecretString> token = await _session.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!token.IsSuccess)
            {
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_apiAddress, "account"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value.Reveal());
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            AccountResponse? account = await response.Content.ReadFromJsonAsync(AccountsJsonContext.Default.AccountResponse, cancellationToken).ConfigureAwait(false);
            open = account?.Features?.Contains("dispatch", StringComparer.Ordinal) == true;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            open = false;
        }
        finally
        {
            Publish(State with { DevicesAvailable = open }, raise: true);
        }
    }

    /// <summary>Returns the GoTrue client, or null when sign-in is not configured. The first call reads <c>GET /auth/config</c>; startup never calls this.</summary>
    /// <param name="cancellationToken">Cancels the configuration read.</param>
    /// <returns>The client, or null.</returns>
    internal async ValueTask<GoTrueClient?> ProvideClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return _client;
        }

        if (Interlocked.Exchange(ref _clientResolved, 1) != 0)
        {
            return _client;
        }

        if (_http is null || _apiAddress is null)
        {
            _configured = false;
            Publish(State with { Configured = false, Phase = AuthPhase.SignedOut }, raise: true);
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_apiAddress, "auth/config"));
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Unavailable();
            }

            AuthConfigResponse? config = await response.Content.ReadFromJsonAsync(AccountsJsonContext.Default.AuthConfigResponse, cancellationToken).ConfigureAwait(false);
            if (config is null || IsNotConfigured(config))
            {
                return Unavailable();
            }

            GoTrueOptionsResult built = GoTrueOptions.Create(config);
            if (!built.IsValid || built.Options is null)
            {
                return Unavailable();
            }

            _client = new GoTrueClient(_http, built.Options, _time, _clientLogger);
            _configured = true;
            return _client;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            return Unavailable();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        if (_operation is not null)
        {
            await _operation.CancelAsync().ConfigureAwait(false);
            _operation.Dispose();
        }
        _session.StateChanged -= OnSessionChanged;
        if (_selector is not null)
        {
            _selector.Changed -= OnSelectorChanged;
            _selector.Dispose();
        }

        if (_flow is not null)
        {
            await _flow.DisposeAsync().ConfigureAwait(false);
        }

        _manager?.Dispose();
        _lifetime.Dispose();
    }

    private GoTrueClient? Unavailable()
    {
        _configured = false;
        Publish(State with { Configured = false }, raise: true);
        return null;
    }

    private static bool IsNotConfigured(AuthConfigResponse config)
    {
        if (string.Equals(config.Mode, "owner-token", StringComparison.OrdinalIgnoreCase)
            || string.Equals(config.Mode, "development", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return config.ProjectUrl is null || string.IsNullOrWhiteSpace(config.PublishableKey);
    }

    private CancellationTokenSource Link(CancellationToken cancellationToken)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        _operation = linked;
        return linked;
    }

    private void ApplyChallenge(AuthResult<SignInChallenge> result)
    {
        if (!result.IsSuccess)
        {
            Publish(State with { Phase = AuthPhase.Error, Error = result.Failure.Kind, Configured = _configured }, raise: true);
            return;
        }

        SignInChallenge challenge = result.Value;
        Publish(new AccountHostState(
            AuthPhase.AwaitingLink,
            challenge.Email,
            false,
            false,
            State.SignOutNotConfirmed,
            State.ServiceChanged,
            false,
            _configured,
            challenge.ExpiresAt,
            challenge.ResendAvailableAt,
            _coordinator?.RemainingAttempts ?? SignInCoordinator.MaxWrongCodes,
            false,
            null,
            !challenge.LinkAvailable), raise: true);
    }

    private void OnSessionChanged(object? sender, AccountStateChangedEventArgs e)
    {
        if (e.State.Phase == AuthPhase.SignedIn)
        {
            NoteSignedIn();
        }
        else if (e.State.Phase == AuthPhase.SignedOut || e.State.Notices.HasFlag(AccountNotices.SessionEnded))
        {
            _settings.SetHasStoredSession(false);
        }

        Publish(FromSession(e.State), raise: true);
    }

    private void OnSelectorChanged(object? sender, EventArgs e) => PersistSelector();

    private void NoteSignedIn()
    {
        if (_selector is null || _selector.HasStoredSession)
        {
            _settings.SetHasStoredSession(true);
        }
    }

    private void PersistSelector()
    {
        if (_selector is null)
        {
            return;
        }

        _settings.SetHasStoredSession(_selector.HasStoredSession);
        _settings.SetAllowFileSecretStore(_selector.AllowFileSecretStore);
    }

    private AccountHostState FromSession(AccountState session)
    {
        var current = State;
        bool offline = session.Phase == AuthPhase.SignedIn && session.Connectivity == AccountConnectivity.Offline;
        return current with
        {
            Phase = session.Phase is AuthPhase.SignedIn or AuthPhase.SignedOut ? session.Phase : current.Phase,
            Email = session.Identity?.Email ?? current.Email,
            Offline = offline,
            SessionNotRemembered = session.Notices.HasFlag(AccountNotices.SessionNotRemembered),
            SignOutNotConfirmed = session.Notices.HasFlag(AccountNotices.SignOutNotConfirmed) || session.Notices.HasFlag(AccountNotices.SignOutNotCleared),
            ServiceChanged = session.Notices.HasFlag(AccountNotices.ServiceChanged),
            Configured = _configured,
        };
    }

    private void Publish(AccountHostState state, bool raise)
    {
        lock (_gate)
        {
            _state = state;
        }

        if (raise)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
