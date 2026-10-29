namespace Autonogrammer.Accounts;

/// <summary>How far the session is known to be in step with the auth service.</summary>
public enum AccountConnectivity
{
    /// <summary>The last refresh succeeded, or the session was just issued.</summary>
    Online,

    /// <summary>A stored session is being refreshed in the background after startup.</summary>
    Refreshing,

    /// <summary>The service could not be reached or no client is configured. The person stays signed in; this is a notice, not a sign-out.</summary>
    Offline,
}
