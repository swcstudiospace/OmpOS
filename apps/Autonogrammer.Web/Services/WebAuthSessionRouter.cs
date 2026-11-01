using Autonogrammer.Presentation.Accounts;
using Autonogrammer.UI.Components.Auth;

namespace Autonogrammer.Web.Services;

/// <summary>
/// Forwards <see cref="IApiTokenProvider"/> and <see cref="IAuthSession"/> to <see cref="OwnerTokenSession"/> until an OIDC configuration with a
/// client block has been accepted, and to <see cref="WebAccountSession"/> afterwards. The mode is known only after <c>GET /auth/config</c>.
/// </summary>
internal sealed class WebAuthSessionRouter : IApiTokenProvider, IAuthSession
{
    private readonly OwnerTokenSession _owner;
    private readonly WebAccountSession _account;
    private bool _oidc;

    /// <summary>Initializes a new instance of the <see cref="WebAuthSessionRouter"/> class.</summary>
    /// <param name="owner">The owner-token session. It is not edited by this plan.</param>
    /// <param name="account">The OIDC account session.</param>
    internal WebAuthSessionRouter(OwnerTokenSession owner, WebAccountSession account)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(account);
        _owner = owner;
        _account = account;
        _owner.Changed += Forward;
        _account.Changed += Forward;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Gets the account host the connect page draws in OIDC mode.</summary>
    internal IAccountHost Account => _account;

    /// <summary>Gets whether the accepted configuration is OIDC with a client block.</summary>
    internal bool UsesAccount => _oidc;

    /// <inheritdoc />
    public bool HasToken => Active.HasToken;

    /// <inheritdoc />
    public bool SessionEnded => Active.SessionEnded;

    /// <inheritdoc />
    public string? ReturnPath
    {
        get => Active.ReturnPath;
        set => Active.ReturnPath = value;
    }

    /// <summary>Records the loaded configuration. A refused origin never selects the account session.</summary>
    /// <param name="config">The configuration.</param>
    /// <param name="builtOrigin">The built auth origin.</param>
    /// <param name="webOrigin">The page origin.</param>
    internal void Apply(AuthConfig config, string? builtOrigin, string? webOrigin)
    {
        ArgumentNullException.ThrowIfNull(config);
        var oidc = string.Equals(config.Mode, AuthConfig.OidcMode, StringComparison.Ordinal)
            && config.HasClientBlock
            && !WebAuthOrigin.Refuses(config, builtOrigin);
        if (oidc)
        {
            _account.Apply(config, builtOrigin, webOrigin);
        }

        if (_oidc == oidc)
        {
            return;
        }

        _oidc = oidc;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken) =>
        (_oidc ? (IApiTokenProvider)_account : _owner).GetTokenAsync(cancellationToken);

    /// <inheritdoc />
    public void Clear() => Active.Clear();

    /// <summary>
    /// Ends the active session after a 401. Each mode acts only on the token the refused request carried, so a late answer for an earlier token never
    /// ends a newer sign-in.
    /// </summary>
    /// <param name="refusedToken">The token the refused request carried, not necessarily the token currently held.</param>
    internal void Expire(string refusedToken)
    {
        if (_oidc)
        {
            _account.Expire(refusedToken);
            return;
        }

        _owner.Expire(refusedToken);
    }

    private IAuthSession Active => _oidc ? _account : _owner;

    private void Forward(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
