namespace Autonogrammer.AgentWorker.Hooks;

/// <summary>
/// The user's consent to project hooks, taken on the desktop and carried to the worker inside the signed connection token (see
/// <see cref="HooksConsentSnapshotReader"/>). It holds the consent identities (hashes, version 2) of the hooks the user approved, who approved
/// them and when. The worker runs a project hook only when its own recomputed identity is in the snapshot, so a hook that was edited after the
/// approval, or one the user never saw, does not run.
/// </summary>
internal sealed record HooksConsentSnapshot
{
    /// <summary>Gets the version of the identity the hashes are computed with. Only the current hook identity version is accepted.</summary>
    public required int Version { get; init; }

    /// <summary>Gets who approved the hooks: the signed-in user's display name or address, as the desktop recorded it.</summary>
    public required string Approver { get; init; }

    /// <summary>Gets when the user approved them.</summary>
    public required DateTimeOffset ApprovedAt { get; init; }

    /// <summary>Gets the lowercase hex SHA-256 identities of the approved hooks.</summary>
    public required IReadOnlyList<string> Hashes { get; init; }
}
