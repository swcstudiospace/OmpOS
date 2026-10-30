using Autonogrammer.UI.Components.Auth;

namespace Autonogrammer.UI.Tests.Auth;

/// <summary>An <see cref="IAuthSession"/> a test drives by hand.</summary>
internal sealed class FakeAuthSession : IAuthSession
{
    private bool _hasToken;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool HasToken => _hasToken;

    /// <inheritdoc />
    public bool SessionEnded { get; set; }

    /// <inheritdoc />
    public string? ReturnPath { get; set; }

    /// <summary>Gets how many times <see cref="Clear"/> ran.</summary>
    internal int ClearCount { get; private set; }

    /// <summary>Makes the session signed in or out and raises <see cref="Changed"/>.</summary>
    /// <param name="hasToken">Whether a token is held.</param>
    internal void SetSignedIn(bool hasToken)
    {
        _hasToken = hasToken;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Clear()
    {
        ClearCount++;
        SetSignedIn(false);
    }
}
