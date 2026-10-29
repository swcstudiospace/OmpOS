namespace Autonogrammer.UI.Components.Auth;

/// <summary>
/// What the shell knows about the sign-in of this browser tab. The token itself stays inside the head (it is never exposed here), is held in memory
/// only and is gone when the tab closes.
/// </summary>
public interface IAuthSession
{
    /// <summary>Occurs after <see cref="HasToken"/> changed.</summary>
    event EventHandler? Changed;

    /// <summary>Gets whether the API accepted a token for this tab.</summary>
    bool HasToken { get; }

    /// <summary>Gets whether the API refused a token that had been accepted before, so the session ended without the user asking. It clears with the next successful connect or sign-out.</summary>
    bool SessionEnded { get; }

    /// <summary>Gets or sets the path (relative to the base address, in memory only) the user asked for before being sent to sign in, or null.</summary>
    string? ReturnPath { get; set; }

    /// <summary>Forgets the token because the user signed out. It does nothing when there is no token.</summary>
    void Clear();
}
