namespace Autonogrammer.UI.Components.Auth;

/// <summary>What came of an attempt to connect with an owner token.</summary>
public enum ConnectOutcome
{
    /// <summary>The API accepted the token. The session holds it.</summary>
    Connected = 0,

    /// <summary>The API refused the token (401 or 403). The session holds nothing.</summary>
    Rejected = 1,

    /// <summary>The API could not be reached or failed. The session holds nothing, and the same token may be tried again.</summary>
    Unreachable = 2,

    /// <summary>The API refused to check the token for now (429): too many failed attempts from this address. The session holds nothing, and the same token may be tried again after a wait.</summary>
    RateLimited = 3,
}
