namespace Autonogrammer.Web.Services.Artifacts;

/// <summary>What an artifact call answered. The two named states are the ones the artifact UI shows in words: sign in, and sync is off.</summary>
internal enum ArtifactCallState
{
    /// <summary>The call succeeded and carries its value.</summary>
    Ok,

    /// <summary>The bearer was refused, so the user must sign in again.</summary>
    SignInRequired,

    /// <summary>The version's content was not stored (metadata-only sync), so there is nothing to show.</summary>
    SyncOff,

    /// <summary>The artifact or version is not visible to the caller.</summary>
    NotFound,

    /// <summary>The account is rate limited; the call may be retried after the stated delay.</summary>
    RateLimited,

    /// <summary>The API could not be reached.</summary>
    Offline,

    /// <summary>Any other refusal or a body that does not match the contract.</summary>
    Failed,
}
