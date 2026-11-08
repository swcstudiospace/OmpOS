namespace Autonogrammer.Presentation.Landing;

/// <summary>
/// The status of omp on this computer, as the landing shows it. The head reads it from the omp resolver; this type keeps the presentation layer free of the launcher.
/// </summary>
/// <param name="State">What the check found.</param>
/// <param name="Version">The version the probe reported, or null when it is unknown.</param>
/// <param name="Source">Where the omp came from, when <see cref="State"/> is <see cref="LocalOmpState.Installed"/>.</param>
/// <param name="Detail">The installation guidance when omp is missing, or the reason when the check threw; otherwise null.</param>
public sealed record LocalOmpStatus(LocalOmpState State, string? Version, LocalOmpSource? Source, string? Detail)
{
    /// <summary>Gets the status before the check has run.</summary>
    public static LocalOmpStatus Pending { get; } = new(LocalOmpState.Pending, null, null, null);

    /// <summary>Gets the status when no check is wired in this window.</summary>
    public static LocalOmpStatus NotChecked { get; } = new(LocalOmpState.NotChecked, null, null, null);

    /// <summary>Gets the status of a runnable omp.</summary>
    /// <param name="version">The reported version, or null.</param>
    /// <param name="source">Where the omp came from.</param>
    /// <returns>The status.</returns>
    public static LocalOmpStatus Installed(string? version, LocalOmpSource source) => new(LocalOmpState.Installed, version, source, null);

    /// <summary>Gets the status when omp is not runnable.</summary>
    /// <param name="guidance">The text that says how to make omp available.</param>
    /// <returns>The status.</returns>
    public static LocalOmpStatus NotInstalled(string guidance) => new(LocalOmpState.NotInstalled, null, null, guidance);

    /// <summary>Gets the status when the check threw.</summary>
    /// <param name="reason">The reason to show.</param>
    /// <returns>The status.</returns>
    public static LocalOmpStatus Failed(string reason) => new(LocalOmpState.Failed, null, null, reason);
}
