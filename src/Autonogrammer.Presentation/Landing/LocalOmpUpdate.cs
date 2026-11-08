namespace Autonogrammer.Presentation.Landing;

/// <summary>
/// The outcome of the managed omp check or install, as the landing shows it. The head maps the launcher's result to this record; this type keeps the
/// presentation layer free of the launcher.
/// </summary>
/// <param name="State">What the check or install found.</param>
/// <param name="Version">The version the notice is about, or null.</param>
/// <param name="CheckedAt">When the registry was last read successfully, for <see cref="LocalOmpUpdateState.Offline"/>; otherwise null.</param>
/// <param name="Reason">The explanation the launcher gave, for the states that carry one; otherwise null.</param>
public sealed record LocalOmpUpdate(LocalOmpUpdateState State, string? Version, DateTimeOffset? CheckedAt, string? Reason)
{
    /// <summary>Gets the outcome when no check has finished.</summary>
    public static LocalOmpUpdate None { get; } = new(LocalOmpUpdateState.None, null, null, null);
}
