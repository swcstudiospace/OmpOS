namespace Autonogrammer.UI.Components.Account;

/// <summary>How a returning tab finished handing the callback to the asking tab.</summary>
public enum AuthCallbackStatus
{
    /// <summary>The address had no sign-in callback.</summary>
    None,

    /// <summary>The asking tab acknowledged the code within three seconds.</summary>
    Acknowledged,

    /// <summary>No acknowledgement arrived within three seconds.</summary>
    Unacknowledged,

    /// <summary>The callback was an error, or the channel was refused.</summary>
    Error,
}
