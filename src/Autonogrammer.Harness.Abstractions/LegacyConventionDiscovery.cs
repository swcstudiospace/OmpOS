namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// Read-only discovery of the convention directory the previous product name used. The order is the current directory, then the
/// legacy directory, then the engine directory. Nothing in this type writes a file.
/// </summary>
public static class LegacyConventionDiscovery
{
    /// <summary>The current convention directory name.</summary>
    public const string NewDirectoryName = ".autonogrammer";

    /// <summary>The engine convention directory name. It is never renamed.</summary>
    public const string EngineDirectoryName = ".omp";

    /// <summary>Gets the legacy convention directory name. It is read and never written.</summary>
    public static string LegacyDirectoryName => LegacyBrandNames.ConventionDirectory;

    /// <summary>
    /// Returns the three candidate directories for a workspace root or a user home, highest precedence first. This method does not
    /// touch the file system.
    /// </summary>
    /// <param name="root">The workspace root or the user home.</param>
    /// <returns>The current directory, the legacy directory, and the engine directory.</returns>
    /// <exception cref="ArgumentException"><paramref name="root"/> is blank.</exception>
    public static IReadOnlyList<string> OrderedCandidates(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return
        [
            Path.Combine(root, NewDirectoryName),
            Path.Combine(root, LegacyDirectoryName),
            Path.Combine(root, EngineDirectoryName),
        ];
    }

    /// <summary>Returns the same order for one file name inside each candidate directory.</summary>
    /// <param name="root">The workspace root or the user home.</param>
    /// <param name="fileName">The file name, such as <c>setup.json</c> or <c>ignore</c>.</param>
    /// <returns>The current path, the legacy path, and the engine path.</returns>
    public static IReadOnlyList<string> OrderedFiles(string root, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return [.. OrderedCandidates(root).Select(directory => Path.Combine(directory, fileName))];
    }

    /// <summary>
    /// Returns a notice when the legacy directory exists and the current directory does not. A tree that has the current directory,
    /// or neither directory, returns an empty list.
    /// </summary>
    /// <param name="root">The workspace root or the user home.</param>
    /// <param name="scope"><c>project</c> or <c>user</c>.</param>
    /// <param name="exists">Reports whether a directory path exists. Tests pass a fake.</param>
    /// <param name="foundFiles">The files found under the legacy directory, or an empty list.</param>
    /// <returns>One notice, or an empty list.</returns>
    public static IReadOnlyList<LegacyNotice> FindLegacyOnly(
        string root,
        string scope,
        Func<string, bool> exists,
        IReadOnlyList<string>? foundFiles = null)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var ordered = OrderedCandidates(root);
        if (!exists(ordered[1]) || exists(ordered[0]))
        {
            return [];
        }

        return [Create(scope, root, foundFiles ?? [], conflict: false)];
    }

    /// <summary>Builds a notice for a legacy directory that was read. A non-empty file list means those files were not merged.</summary>
    /// <param name="scope"><c>project</c> or <c>user</c>.</param>
    /// <param name="root">The workspace root or the user home.</param>
    /// <param name="conflictingFiles">The legacy files ignored because the current directory also has them.</param>
    /// <param name="conflict">True when the current directory also exists.</param>
    /// <returns>The notice.</returns>
    public static LegacyNotice Create(string scope, string root, IReadOnlyList<string> conflictingFiles, bool conflict)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(conflictingFiles);
        var ordered = OrderedCandidates(root);
        return new LegacyNotice(scope, ordered[1], ordered[0], MoveCommand(scope), conflictingFiles, conflict);
    }

    /// <summary>
    /// Returns the documented move text. A project scope uses <c>git mv</c>. A user scope names a directory move and never a git command.
    /// </summary>
    /// <param name="scope"><c>project</c> or <c>user</c>.</param>
    /// <returns>The command text.</returns>
    public static string MoveCommand(string scope) =>
        string.Equals(scope, "user", StringComparison.Ordinal)
            ? "mv -- " + LegacyDirectoryName + " " + NewDirectoryName
            : "git mv -- " + LegacyDirectoryName + " " + NewDirectoryName;
}

#pragma warning disable MA0048 // The plan keeps the notice record beside the discovery type.
/// <summary>A one-time notice that a legacy convention directory was read and must not be written.</summary>
/// <param name="Scope"><c>project</c> or <c>user</c>.</param>
/// <param name="LegacyPath">The legacy directory that was read.</param>
/// <param name="NewPath">The current directory the files should move to.</param>
/// <param name="MoveCommand">The documented move text for the scope.</param>
/// <param name="ConflictingFiles">Legacy files that were not merged because the current directory won.</param>
/// <param name="Conflict">True when the current directory also exists, or when a legacy safety file is reported rather than ignored.</param>
public sealed record LegacyNotice(
    string Scope,
    string LegacyPath,
    string NewPath,
    string MoveCommand,
    IReadOnlyList<string> ConflictingFiles,
    bool Conflict);
