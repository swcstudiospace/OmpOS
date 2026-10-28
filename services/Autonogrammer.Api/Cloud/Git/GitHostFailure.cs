namespace Autonogrammer.Api.Cloud;

/// <summary>Why a git host call failed.</summary>
internal enum GitHostFailure
{
    /// <summary>No credential is configured.</summary>
    NotConfigured,

    /// <summary>The host refused the credential or the credential lacks the permission.</summary>
    Unauthorized,

    /// <summary>The repository, branch or file does not exist, or the credential cannot see it.</summary>
    NotFound,

    /// <summary>The host refused the request as invalid (for example the branch has no commits ahead of the base).</summary>
    Rejected,

    /// <summary>A pull request for the branch already exists.</summary>
    AlreadyExists,

    /// <summary>The host did not answer, answered with a server error, or answered with something unreadable.</summary>
    Unavailable,

    /// <summary>The request cannot be served (for example repositories of several installations in one token).</summary>
    Unsupported,
}
