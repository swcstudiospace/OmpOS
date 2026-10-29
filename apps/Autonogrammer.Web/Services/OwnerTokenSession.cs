using Autonogrammer.UI.Components.Auth;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The owner token of this browser tab: the <see cref="IApiTokenProvider"/> of a hosted API in owner-token mode and the <see cref="IAuthSession"/> the
/// shell reads. The token lives in one private field. It is never logged, displayed, put in a URL or written to any browser storage, and it is gone
/// when the tab closes. A token is held in two steps: <see cref="Offer"/> makes it the one the next requests send, and <see cref="Confirm"/> says the
/// API accepted it, which is when the shell treats the tab as signed in.
/// </summary>
internal sealed class OwnerTokenSession : IApiTokenProvider, IAuthSession
{
    private string? _token;
    private bool _confirmed;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool HasToken => _confirmed;

    /// <inheritdoc />
    public bool SessionEnded { get; private set; }

    /// <inheritdoc />
    public string? ReturnPath { get; set; }

    /// <inheritdoc />
    public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_token);

    /// <summary>Makes a token the one the next requests send, without signing in: it has not been checked yet.</summary>
    /// <param name="token">The token. It is never logged.</param>
    internal void Offer(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _token = token;
        _confirmed = false;
    }

    /// <summary>Records that the API accepted the offered token. The tab is signed in, and any "session ended" notice is gone.</summary>
    /// <returns>True when the token was confirmed; false when none is held any more (the API refused it in the meantime).</returns>
    internal bool Confirm()
    {
        if (_token is null)
        {
            return false;
        }

        var changed = !_confirmed || SessionEnded;
        _confirmed = true;
        SessionEnded = false;
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    /// <summary>Holds a token as accepted in one step (<see cref="Offer"/> then <see cref="Confirm"/>).</summary>
    /// <param name="token">The token. It is never logged.</param>
    internal void SetToken(string token)
    {
        Offer(token);
        _ = Confirm();
    }

    /// <inheritdoc />
    public void Clear()
    {
        var wasConfirmed = _confirmed;
        _token = null;
        _confirmed = false;
        SessionEnded = false;
        if (wasConfirmed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Forgets the token because the API refused it (401). It does nothing when no token is held, so a burst of 401 answers ends the session once. A token
    /// that had been accepted ends the session with a notice; one that was only offered is dropped silently, because the connect page reports it itself.
    /// </summary>
    internal void Expire()
    {
        if (_token is null)
        {
            return;
        }

        var wasConfirmed = _confirmed;
        _token = null;
        _confirmed = false;
        if (!wasConfirmed)
        {
            return;
        }

        SessionEnded = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// <see cref="Expire()"/> for the answer to one request: it acts only when the refused token is still the one held. A late 401 for a request that was
    /// sent with an old token (a stream that was open when the token was replaced) is ignored instead of clearing the newer token.
    /// </summary>
    /// <param name="refusedToken">The token the refused request carried.</param>
    internal void Expire(string refusedToken)
    {
        if (string.Equals(_token, refusedToken, StringComparison.Ordinal))
        {
            Expire();
        }
    }
}
