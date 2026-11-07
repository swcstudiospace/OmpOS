#pragma warning disable MA0048 // The plan owns this file for the store and its file document.
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Autonogrammer.Cowork.Projects;

/// <summary>
/// The ADR-0032 project file at <c>&lt;stateRoot&gt;/cowork/&lt;account&gt;/projects.json</c>, extended with D-03 fields.
/// <c>folder</c> is kept and mapped to <see cref="CoworkProject.Folders"/> index 0. Unknown members are kept.
/// A corrupt file is moved aside and an empty set is returned. Hard delete never touches a folder.
/// </summary>
public sealed class CoworkProjectStore
{
    /// <summary>The file name inside the account folder.</summary>
    public const string FileName = "projects.json";

    /// <summary>The largest file that is read or written. Two hundred projects at the instruction cap need more than 256 KiB.</summary>
    public const int MaxFileBytes = 8 * 1024 * 1024;

    /// <summary>The notice when a corrupt file was moved aside.</summary>
    public const string ProjectFileCorrupt = "ProjectFileCorrupt";

    /// <summary>The notice when this device has no folder for the project.</summary>
    public const string FolderMissing = "FolderMissing";

    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="CoworkProjectStore"/> class.</summary>
    /// <param name="stateRoot">The state directory.</param>
    /// <param name="accountFolderKey">The account folder key.</param>
    public CoworkProjectStore(string stateRoot, string accountFolderKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountFolderKey);
        _path = Path.Combine(global::Autonogrammer.Cowork.CoworkAccountFolder.AccountDirectory(stateRoot, accountFolderKey), FileName);
    }

    /// <summary>Gets the full path of the file.</summary>
    public string FilePath => _path;

    /// <summary>Gets the path a corrupt file was moved to, or null.</summary>
    public string? MovedAsidePath { get; private set; }

    /// <summary>Gets the last read notice, or null.</summary>
    public string? LastNotice { get; private set; }

    /// <summary>Returns active projects. A corrupt file yields an empty set and <see cref="ProjectFileCorrupt"/>.</summary>
    /// <returns>The active projects.</returns>
    public IReadOnlyList<CoworkProject> List() => List(includeArchived: false);

    /// <summary>Returns projects. Archived projects are hidden unless requested.</summary>
    /// <param name="includeArchived">Whether archived projects are included.</param>
    /// <returns>The projects.</returns>
    public IReadOnlyList<CoworkProject> List(bool includeArchived)
    {
        var document = ReadDocument();
        if (document.Projects is null)
        {
            return [];
        }

        var projects = document.Projects.Select(ToProject);
        if (!includeArchived)
        {
            projects = projects.Where(static project => project.ArchivedAt is null);
        }

        return [.. projects];
    }

    /// <summary>Inserts or replaces a project. Limits are enforced. Unknown members on that project are kept.</summary>
    /// <param name="project">The project.</param>
    /// <exception cref="InvalidOperationException">The project or the account count breaks a D-03 limit.</exception>
    public void Upsert(CoworkProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var errors = ProjectValidator.Validate(project);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(' ', errors));
        }

        lock (_gate)
        {
            var document = ReadDocumentUnlocked();
            document.Projects ??= [];
            var index = IndexOf(document, project.Id);
            if (index < 0 && ProjectValidator.ValidateCount(document.Projects.Count + 1).Count > 0)
            {
                throw new InvalidOperationException(string.Join(' ', ProjectValidator.ValidateCount(document.Projects.Count + 1)));
            }

            var entry = ToEntry(project, index >= 0 ? document.Projects[index].Extra : null);
            if (index >= 0)
            {
                document.Projects[index] = entry;
            }
            else
            {
                document.Projects.Add(entry);
            }

            Write(document);
        }
    }

    /// <summary>Renames a project. Other fields stay.</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="name">The new name.</param>
    /// <returns>False when the project is missing.</returns>
    public bool Rename(string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var current = Find(id);
        if (current is null)
        {
            return false;
        }

        Upsert(current with { Name = name, UpdatedAt = DateTimeOffset.UtcNow });
        return true;
    }

    /// <summary>Archives a project. Memory and files stay. The project is hidden from <see cref="List()"/>.</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="archivedAt">When it was archived.</param>
    /// <returns>False when the project is missing.</returns>
    public bool Archive(string id, DateTimeOffset archivedAt)
    {
        var current = Find(id);
        if (current is null)
        {
            return false;
        }

        Upsert(current with { ArchivedAt = archivedAt, UpdatedAt = archivedAt });
        return true;
    }

    /// <summary>
    /// Hard-deletes project metadata only after the confirmation carries both counts.
    /// Never deletes a folder on disk.
    /// </summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="confirmation">The counts the user confirmed.</param>
    /// <param name="memoryCount">The project-memory count the store was told.</param>
    /// <returns>True when metadata was removed.</returns>
    public bool TryHardDelete(string id, ProjectDeleteConfirmation confirmation, int memoryCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(confirmation);
        if (confirmation.MetadataCount != 1 || confirmation.MemoryCount != memoryCount)
        {
            return false;
        }

        lock (_gate)
        {
            var document = ReadDocumentUnlocked();
            if (document.Projects is null)
            {
                return false;
            }

            var index = IndexOf(document, id);
            if (index < 0)
            {
                return false;
            }

            document.Projects.RemoveAt(index);
            Write(document);
            return true;
        }
    }

    /// <summary>Describes the workspace for a project. A missing folder is <see cref="FolderMissing"/>, not an exception.</summary>
    /// <param name="project">The project.</param>
    /// <returns>The workspace notice.</returns>
    public static ProjectWorkspace Workspace(CoworkProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var folder = project.Folders.Count > 0 ? project.Folders[0] : null;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return new ProjectWorkspace(null, FolderMissing);
        }

        return new ProjectWorkspace(folder, null);
    }

    private CoworkProject? Find(string id)
    {
        foreach (var project in List(includeArchived: true))
        {
            if (string.Equals(project.Id, id, StringComparison.Ordinal))
            {
                return project;
            }
        }

        return null;
    }

    private ProjectFileDocument ReadDocument()
    {
        lock (_gate)
        {
            return ReadDocumentUnlocked();
        }
    }

    private ProjectFileDocument ReadDocumentUnlocked()
    {
        LastNotice = null;
        MovedAsidePath = null;
        if (!File.Exists(_path))
        {
            return new ProjectFileDocument();
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(_path);
        }
        catch (IOException)
        {
            return MoveAside();
        }

        if (bytes.Length > MaxFileBytes)
        {
            return MoveAside();
        }

        try
        {
            var document = JsonSerializer.Deserialize(bytes, LocalProjectJsonContext.Default.ProjectFileDocument);
            if (document is null)
            {
                return MoveAside();
            }

            document.Projects ??= [];
            foreach (var entry in document.Projects)
            {
                MapFolder(entry);
            }

            return document;
        }
        catch (JsonException)
        {
            return MoveAside();
        }
    }

    private ProjectFileDocument MoveAside()
    {
        var aside = _path + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        try
        {
            if (File.Exists(_path))
            {
                File.Move(_path, aside, overwrite: false);
                MovedAsidePath = aside;
            }
        }
        catch (IOException)
        {
            MovedAsidePath = _path;
        }

        LastNotice = ProjectFileCorrupt;
        return new ProjectFileDocument();
    }

    private void Write(ProjectFileDocument document)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, LocalProjectJsonContext.Default.ProjectFileDocument);
        if (bytes.Length > MaxFileBytes)
        {
            throw new InvalidDataException("The project file would exceed the size cap.");
        }

        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var temporary = _path + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(temporary, _path, overwrite: true);
    }

    private static void MapFolder(ProjectFileEntry entry)
    {
        entry.Folders ??= [];
        if (!string.IsNullOrWhiteSpace(entry.Folder) && !entry.Folders.Contains(entry.Folder, StringComparer.Ordinal))
        {
            entry.Folders.Insert(0, entry.Folder);
        }
        else if (entry.Folders.Count > 0)
        {
            entry.Folder = entry.Folders[0];
        }
    }

    private static int IndexOf(ProjectFileDocument document, string id)
    {
        if (document.Projects is null)
        {
            return -1;
        }

        for (var index = 0; index < document.Projects.Count; index++)
        {
            if (string.Equals(document.Projects[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static int MaxCount(ProjectFileDocument document) => document.Projects?.Count ?? 0;

    private static ProjectFileEntry ToEntry(CoworkProject project, Dictionary<string, JsonElement>? extra)
    {
        var folders = project.Folders.ToList();
        return new ProjectFileEntry
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Instructions = project.Instructions,
            ContextUrls = [.. project.ContextUrls],
            Folder = folders.Count > 0 ? folders[0] : null,
            Folders = folders,
            UseAccountMemory = project.UseAccountMemory,
            ArchivedAt = project.ArchivedAt,
            UpdatedAt = project.UpdatedAt,
            Extra = extra,
        };
    }

    private static CoworkProject ToProject(ProjectFileEntry entry)
    {
        MapFolder(entry);
        return new CoworkProject
        {
            Id = entry.Id ?? string.Empty,
            Name = entry.Name ?? string.Empty,
            Description = entry.Description,
            Instructions = entry.Instructions,
            ContextUrls = entry.ContextUrls ?? [],
            Folders = entry.Folders ?? [],
            UseAccountMemory = entry.UseAccountMemory,
            ArchivedAt = entry.ArchivedAt,
            UpdatedAt = entry.UpdatedAt,
        };
    }
}

/// <summary>The counts a hard delete must show before metadata is removed.</summary>
/// <param name="MetadataCount">The metadata rows that will be removed.</param>
/// <param name="MemoryCount">The project-memory rows that will be removed.</param>
public sealed record ProjectDeleteConfirmation(int MetadataCount, int MemoryCount);

/// <summary>A session workspace, or <see cref="CoworkProjectStore.FolderMissing"/>.</summary>
/// <param name="Folder">The folder, or null.</param>
/// <param name="Notice">The notice, or null when a folder is present.</param>
public sealed record ProjectWorkspace(string? Folder, string? Notice);

internal sealed class ProjectFileDocument
{
    public int SchemaVersion { get; set; } = 1;

    public List<ProjectFileEntry>? Projects { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal sealed class ProjectFileEntry
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Instructions { get; set; }

    public List<string>? ContextUrls { get; set; }

    public string? Folder { get; set; }

    public List<string>? Folders { get; set; }

    public bool UseAccountMemory { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProjectFileDocument))]
internal sealed partial class LocalProjectJsonContext : JsonSerializerContext
{
}
