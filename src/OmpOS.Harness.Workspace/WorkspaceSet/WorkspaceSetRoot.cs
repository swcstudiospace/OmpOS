namespace OmpOS.Workspace.Sets;

/// <summary>
/// One folder of a <see cref="WorkspaceSet"/>. This is a value that names a folder; it is distinct from
/// <see cref="WorkspaceRoot"/>, which is the symlink-safe handle for one existing local directory.
/// </summary>
/// <param name="Id">A stable identifier of the root inside its set: lowercase letters, digits and hyphens.</param>
/// <param name="Path">The absolute path of the folder, as the environment that owns it spells it.</param>
/// <param name="DisplayName">The name shown to the user and used as the root-qualified prefix of its files (<c>name/relative</c>).</param>
/// <param name="EnvironmentId">The environment profile that owns the folder, or null for this machine.</param>
/// <param name="IsPrimary">True for the root that becomes the working directory of omp. A set has exactly one.</param>
public sealed record WorkspaceSetRoot(string Id, string Path, string DisplayName, string? EnvironmentId = null, bool IsPrimary = false)
{
    /// <summary>Gets whether the folder is local or lives in a remote environment.</summary>
    public WorkspaceRootKind Kind => EnvironmentId is null ? WorkspaceRootKind.Local : WorkspaceRootKind.Remote;
}
