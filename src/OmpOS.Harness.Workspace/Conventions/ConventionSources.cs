namespace OmpOS.Workspace.Conventions;

/// <summary>The two file trees a discovery run reads: the one that holds the workspace roots and the one that holds the user's home.</summary>
/// <param name="Project">The source of the workspace roots.</param>
/// <param name="User">The source of the user's home directory. For a remote workspace it is usually still the local machine.</param>
public sealed record ConventionSources(IConventionFileSource Project, IConventionFileSource User)
{
    /// <summary>Creates the sources of a local session: both trees are the local disk, each limited to its own roots.</summary>
    /// <param name="roots">The workspace roots.</param>
    /// <param name="userHome">The user's home directory, or null for none.</param>
    /// <returns>The sources.</returns>
    public static ConventionSources Local(IReadOnlyList<string> roots, string? userHome)
    {
        ArgumentNullException.ThrowIfNull(roots);
        return new ConventionSources(
            new LocalConventionFileSource(roots),
            new LocalConventionFileSource(userHome is null ? Array.Empty<string>() : new[] { userHome }));
    }
}
