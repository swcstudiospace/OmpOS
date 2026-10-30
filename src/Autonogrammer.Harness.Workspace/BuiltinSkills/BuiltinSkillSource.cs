using Autonogrammer.Workspace.Conventions;

namespace Autonogrammer.Workspace.BuiltinSkills;

/// <summary>
/// The skills that ship inside the application: one folder per skill, each with a <c>SKILL.md</c>, in a <c>skills</c> folder next to the application. It is an
/// <see cref="IConventionFileSource"/> limited to that folder, so discovery can read it exactly like a workspace. A folder that does not exist (a head that does not bundle the
/// skills, or a trimmed install) lists nothing and is not an error.
/// </summary>
public sealed class BuiltinSkillSource : IConventionFileSource
{
    /// <summary>The name of the folder, below <see cref="BasePath"/>, that holds one folder per skill.</summary>
    public const string FolderName = "skills";

    private readonly LocalConventionFileSource _files;

    /// <summary>Creates a source over the <c>skills</c> folder of a base directory.</summary>
    /// <param name="basePath">The directory that holds the <c>skills</c> folder. The application passes its base directory; a test passes a temporary one.</param>
    /// <exception cref="ArgumentException"><paramref name="basePath"/> is null, empty or white space.</exception>
    public BuiltinSkillSource(string basePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        BasePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(basePath));
        SkillsPath = Path.Combine(BasePath, FolderName);
        _files = new LocalConventionFileSource([SkillsPath]);
    }

    /// <summary>Gets the directory that holds the <c>skills</c> folder.</summary>
    public string BasePath { get; }

    /// <summary>Gets the full path of the <c>skills</c> folder.</summary>
    public string SkillsPath { get; }

    /// <summary>Gets a value indicating whether the <c>skills</c> folder exists.</summary>
    public bool FolderExists => Directory.Exists(SkillsPath);

    /// <summary>Creates the source of the running application: the <c>skills</c> folder in the application's base directory.</summary>
    /// <returns>The source.</returns>
    public static BuiltinSkillSource CreateDefault() => new(AppContext.BaseDirectory);

    /// <inheritdoc/>
    public Task<ConventionListResult> ListAsync(string directory, int maxEntries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        return FolderExists ? _files.ListAsync(directory, maxEntries, cancellationToken) : Task.FromResult(ConventionListResult.Empty);
    }

    /// <inheritdoc/>
    public Task<ConventionReadResult> ReadTextAsync(string path, int maxBytes, CancellationToken cancellationToken) =>
        _files.ReadTextAsync(path, maxBytes, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken) => _files.ExistsAsync(path, cancellationToken);
}
