namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The record of the managed install, stored as <c>state.json</c>. The active executable is trusted only while its SHA-256
/// matches <see cref="ActiveSha256"/>.
/// </summary>
/// <param name="ActiveVersion">The version that launches, or null when no managed install is active.</param>
/// <param name="ActiveSha256">The hex SHA-256 of the active executable at activation.</param>
/// <param name="PreviousGood">The version that was active before the current one, to restore on failure.</param>
/// <param name="Quarantined">The versions that failed their health check. They are not installed again.</param>
/// <param name="LastCheckedAt">When the registry was last read successfully.</param>
public sealed record OmpManagedState(
    string? ActiveVersion,
    string? ActiveSha256,
    string? PreviousGood,
    IReadOnlyList<OmpQuarantinedVersion>? Quarantined,
    DateTimeOffset? LastCheckedAt)
{
    /// <summary>A state with no managed install.</summary>
    public static OmpManagedState Empty { get; } = new(null, null, null, null, null);

    /// <summary>Reports whether <paramref name="version"/> is quarantined.</summary>
    /// <param name="version">The version to check.</param>
    /// <returns>True when the version is quarantined.</returns>
    public bool IsQuarantined(string version) =>
        Quarantined?.Any(entry => string.Equals(entry.Version, version, StringComparison.Ordinal)) == true;
}
