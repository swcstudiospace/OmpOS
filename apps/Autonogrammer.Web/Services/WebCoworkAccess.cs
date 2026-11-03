using Autonogrammer.UI.Components.Auth;
using Autonogrammer.UI.Components.Cowork;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The Web answer to <see cref="ICoworkAccess"/>. The Demo is never available. Every other head waits for <c>GET /auth/config</c>,
/// then is available in development mode or while a session is held. It offers sign-in only where the deployment signs in with OIDC and serves a client block.
/// </summary>
internal sealed class WebCoworkAccess : ICoworkAccess, IDisposable
{
    private readonly bool _demo;
    private readonly IAuthSession? _session;
    private readonly Task _resolved;
    private AuthConfig? _config;
    private bool _ready;

    /// <summary>Creates the access port and starts the config load. A Demo head resolves at once.</summary>
    /// <param name="options">Whether this head is the Demo.</param>
    /// <param name="session">The tab session, or null when the head has none.</param>
    /// <param name="config">The auth config source, or null when the head has none.</param>
    internal WebCoworkAccess(WebWorkbenchOptions options, IAuthSession? session, IAuthConfigSource? config)
    {
        ArgumentNullException.ThrowIfNull(options);
        _demo = options.IsDemo;
        _session = session;
        if (session is not null)
        {
            session.Changed += OnSessionChanged;
        }

        _resolved = _demo ? Task.CompletedTask : LoadAsync(config);
        if (_demo)
        {
            _ready = true;
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed;


    /// <inheritdoc />
    public bool IsResolved => _ready;

    /// <inheritdoc />
    public bool IsAvailable => _ready && !_demo && (_session?.HasToken == true || IsDevelopment);

    /// <inheritdoc />
    public bool OffersSignIn => _ready && !_demo && !IsAvailable && NeedsSignIn;

    /// <inheritdoc />
    public bool LoadFailed { get; set; }

    /// <inheritdoc />
    public Task RetryAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_session is not null)
        {
            _session.Changed -= OnSessionChanged;
        }
    }

    private bool IsDevelopment => string.Equals(_config?.Mode, AuthConfig.DevelopmentMode, StringComparison.Ordinal);

    private bool NeedsSignIn =>
        _config is { Mode: AuthConfig.OidcMode } && _config.HasClientBlock;

    private async Task LoadAsync(IAuthConfigSource? config)
    {
        if (config is not null)
        {
            try
            {
                _config = await config.LoadAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception fault) when (fault is not OutOfMemoryException)
            {
                _config = null;
            }
        }

        _ready = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
