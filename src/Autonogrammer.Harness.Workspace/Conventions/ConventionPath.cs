namespace Autonogrammer.Workspace.Conventions;

/// <summary>Joins source paths. Every source uses <c>/</c>, so the join never depends on the host operating system.</summary>
internal static class ConventionPath
{
    /// <summary>Joins a folder and a name.</summary>
    /// <param name="directory">The folder path.</param>
    /// <param name="name">The name to append.</param>
    /// <returns>The joined path.</returns>
    public static string Join(string directory, string name) =>
        directory.Length == 0 ? name
        : directory[^1] is '/' or '\\' ? directory + name
        : directory + "/" + name;

    /// <summary>Joins a relative display prefix and a name; an empty prefix yields the name.</summary>
    /// <param name="prefix">The relative path so far.</param>
    /// <param name="name">The name to append.</param>
    /// <returns>The joined relative path.</returns>
    public static string JoinRelative(string prefix, string name) => prefix.Length == 0 ? name : name.Length == 0 ? prefix : prefix + "/" + name;
}
