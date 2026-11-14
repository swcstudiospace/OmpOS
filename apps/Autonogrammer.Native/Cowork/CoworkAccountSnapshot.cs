using Autonogrammer.Cowork.Memory;
using Autonogrammer.Cowork.Projects;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// The account data one interactive Cowork session takes at its start: the global instructions, the instructions of the session's project and the fenced
/// memory brief. Every value belongs to <see cref="AccountKey"/>. A snapshot that is unavailable carries no account data.
/// </summary>
/// <param name="AccountKey">The folder key of the account the data belongs to, or null when there is none.</param>
/// <param name="SyncState">The <see cref="ProjectSyncState"/> of the sync that preceded the snapshot.</param>
/// <param name="IsReadOnly">Whether the data is the last synced copy and is read-only.</param>
/// <param name="GlobalInstructions">The account's global instructions, or null.</param>
/// <param name="ProjectId">The session's project, or null when it has none, or it is missing or archived.</param>
/// <param name="ProjectState">One of the <c>Project*</c> constants of this type.</param>
/// <param name="ProjectInstructions">The project's instructions, or null.</param>
/// <param name="MemoryState">A <see cref="MemoryCodes"/> value, or one of the memory constants of this type.</param>
/// <param name="MemoryBrief">The fenced memory brief, or an empty string.</param>
internal sealed record CoworkAccountSnapshot(
    string? AccountKey,
    string SyncState,
    bool IsReadOnly,
    string? GlobalInstructions,
    string? ProjectId,
    string ProjectState,
    string? ProjectInstructions,
    string MemoryState,
    string MemoryBrief)
{
    /// <summary>The session is not linked to a project.</summary>
    public const string ProjectNone = "ProjectNone";

    /// <summary>The linked project is not on this device.</summary>
    public const string ProjectMissing = "ProjectMissing";

    /// <summary>The linked project is active.</summary>
    public const string ProjectActive = "ProjectActive";

    /// <summary>The linked project is archived. Its instructions and its memory are not used.</summary>
    public const string ProjectArchived = "ProjectArchived";

    /// <summary>The account has memory disabled.</summary>
    public const string MemoryDisabled = "MemoryDisabled";

    /// <summary>The session record could not be read, so memory stays off for the session.</summary>
    public const string MemoryOffUnreadableSession = "MemoryOffUnreadableSession";

    /// <summary>Returns the snapshot for a request that has no account: signed out, switched during the call, or refused by the sync.</summary>
    /// <param name="state">The <see cref="ProjectSyncState"/> that explains why.</param>
    /// <returns>A snapshot with no account and no data.</returns>
    public static CoworkAccountSnapshot Unavailable(string state) =>
        new(null, state, true, null, null, ProjectNone, null, MemoryCodes.SignedOut, string.Empty);
}
