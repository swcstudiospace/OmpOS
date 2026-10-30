
namespace Autonogrammer.Cowork.Sessions;

/// <summary>
/// The local project list for one account. The file is <c>&lt;stateRoot&gt;/cowork/&lt;account&gt;/projects.json</c>.
/// Writes are atomic and owner-only. Unknown JSON members are kept. A file over the size cap is not overwritten.
/// Project memory, sharing and scheduling are out of scope.
/// </summary>
public sealed class CoworkProjectStore
{
    /// <summary>The file name inside the account folder.</summary>
    public const string FileName = "projects.json";

    /// <summary>The largest file that is read or written, in bytes.</summary>
    public const int MaxFileBytes = 256 * 1024;

    private readonly CoworkJsonFile<CoworkProjectDocument> _file;

    /// <summary>Initializes a new instance of the <see cref="CoworkProjectStore"/> class.</summary>
    /// <param name="stateRoot">The state directory.</param>
    /// <param name="accountFolderKey">The account folder key.</param>
    public CoworkProjectStore(string stateRoot, string accountFolderKey)
    {
        _file = new CoworkJsonFile<CoworkProjectDocument>(
            Path.Combine(CoworkAccountFolder.AccountDirectory(stateRoot, accountFolderKey), FileName),
            MaxFileBytes,
            static () => new CoworkProjectDocument(),
            CoworkProjectJsonContext.Default.CoworkProjectDocument);
    }

    /// <summary>Gets the full path of the file.</summary>
    public string FilePath => _file.FilePath;

    /// <summary>Returns the projects.</summary>
    /// <returns>The saved projects.</returns>
    /// <exception cref="InvalidDataException">The file is unreadable, not valid, or larger than <see cref="MaxFileBytes"/>.</exception>
    public IReadOnlyList<CoworkProject> List()
    {
        var document = _file.Read();
        return document.Projects is null ? [] : [.. document.Projects.Select(ToProject)];
    }

    /// <summary>Inserts or replaces a project by identifier. Unknown members on the file and on that project are kept.</summary>
    /// <param name="project">The project.</param>
    /// <exception cref="InvalidDataException">The file cannot be read or the replacement would exceed <see cref="MaxFileBytes"/>.</exception>
    public void Upsert(CoworkProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(project.Id))
        {
            throw new ArgumentException("The project id is required.", nameof(project));
        }
        _file.Update(document =>
        {
            document.Projects ??= [];
            var index = document.Projects.FindIndex(entry => string.Equals(entry.Id, project.Id, StringComparison.Ordinal));
            var entry = new CoworkProjectEntry
            {
                Id = project.Id,
                Name = project.Name,
                Folder = project.Folder,
                Instructions = project.Instructions,
                Extra = index >= 0 ? document.Projects[index].Extra : null,
            };
            if (index >= 0)
            {
                document.Projects[index] = entry;
            }
            else
            {
                document.Projects.Add(entry);
            }
        });
    }

    private static CoworkProject ToProject(CoworkProjectEntry entry) =>
        new()
        {
            Id = entry.Id ?? string.Empty,
            Name = entry.Name ?? string.Empty,
            Folder = entry.Folder,
            Instructions = entry.Instructions,
        };
}

