using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// The model behind the workspace set picker: the folders one session covers, with add folder, remove, reorder, make primary,
/// rename, and import and export of <c>.code-workspace</c> files. Every change is checked again by the backend, so each row shows
/// its own findings and <see cref="CanStart"/> says whether the set may start a session. The model holds the user's input as typed,
/// including a combination the rules reject, so the user can see why and fix it.
/// </summary>
public sealed class WorkspaceSetPicker : ObservableModel
{
    private const string FallbackName = "root";

    private readonly IWorkspaceSetPickerBackend _backend;
    private readonly ObservableCollection<WorkspacePickerRow> _rows = [];
    private IReadOnlyList<WorkspacePickerIssue> _issues = [];
    private IReadOnlyList<string> _importWarnings = [];
    private string _name;
    private string? _lastError;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceSetPicker"/> class with no folder.
    /// </summary>
    /// <param name="backend">The validation and file access of the host.</param>
    /// <param name="name">The name of the set.</param>
    public WorkspaceSetPicker(IWorkspaceSetPickerBackend backend, string name = "Workspace")
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _backend = backend;
        _name = name;
        Rows = new ReadOnlyObservableCollection<WorkspacePickerRow>(_rows);
    }

    /// <summary>Gets the folders in order; the primary is first.</summary>
    public ReadOnlyObservableCollection<WorkspacePickerRow> Rows { get; }

    /// <summary>Gets or sets the name of the set. A blank value is ignored.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                SetProperty(ref _name, value.Trim());
            }
        }
    }

    /// <summary>Gets every finding from the last check, errors and warnings, including the ones about single rows.</summary>
    public IReadOnlyList<WorkspacePickerIssue> Issues => _issues;

    /// <summary>Gets whether the set has at least one folder and no blocking finding.</summary>
    public bool CanStart => _rows.Count > 0 && !_issues.Any(issue => issue.IsError);

    /// <summary>Gets whether the last check produced a warning.</summary>
    public bool HasWarnings => _issues.Any(issue => !issue.IsError);

    /// <summary>Gets the sentence that explains why the last import or export did not happen, or null after a success.</summary>
    public string? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    /// <summary>Gets the entries of the last imported file that could not become folders.</summary>
    public IReadOnlyList<string> ImportWarnings
    {
        get => _importWarnings;
        private set => SetProperty(ref _importWarnings, value);
    }

    /// <summary>Returns the folders as plain data, primary first, ready to build a workspace set from.</summary>
    /// <returns>The roots.</returns>
    public IReadOnlyList<WorkspacePickerRoot> Snapshot() => _rows.Select(row => row.Root).ToArray();

    /// <summary>Replaces the folders, for example with a remembered recent set.</summary>
    /// <param name="name">The name of the set.</param>
    /// <param name="roots">The folders. The one flagged primary moves to the front; with none flagged the first is the primary.</param>
    /// <exception cref="ArgumentException">More than one root is flagged primary, or an identifier repeats.</exception>
    public void Load(string name, IReadOnlyList<WorkspacePickerRoot> roots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Count(root => root.IsPrimary) > 1)
        {
            throw new ArgumentException("A set has exactly one primary folder.", nameof(roots));
        }

        if (roots.Select(root => root.Id).Distinct(StringComparer.Ordinal).Count() != roots.Count)
        {
            throw new ArgumentException("Folder identifiers must be unique.", nameof(roots));
        }

        Name = name;
        Apply(WithPrimaryFirst(roots.ToList()));
        LastError = null;
        ImportWarnings = [];
    }

    /// <summary>
    /// Adds a folder at the end. It is the primary when the picker is empty. Its name is the last path segment, made unique among the rows.
    /// </summary>
    /// <param name="path">The folder path. It is kept as given; validation judges it.</param>
    /// <exception cref="ArgumentException">The path is blank.</exception>
    public void AddFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var roots = Snapshot().ToList();
        var names = new HashSet<string>(roots.Select(root => root.DisplayName), StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(roots.Select(root => root.Id), StringComparer.Ordinal);
        var name = Unique(DefaultName(path), names);
        var id = Unique(Slug(name), ids);
        roots.Add(new WorkspacePickerRoot(id, path, name, roots.Count == 0 ? null : roots[0].EnvironmentId, IsPrimary: roots.Count == 0));
        Apply(roots);
    }

    /// <summary>
    /// Removes a folder. Removing the primary makes the next folder the primary.
    /// </summary>
    /// <param name="rootId">The identifier of the folder.</param>
    /// <returns>True when a folder was removed.</returns>
    public bool Remove(string rootId)
    {
        ArgumentNullException.ThrowIfNull(rootId);
        var roots = Snapshot().ToList();
        var removed = roots.RemoveAll(root => string.Equals(root.Id, rootId, StringComparison.Ordinal)) > 0;
        if (removed)
        {
            Apply(WithPrimaryFirst(roots));
        }

        return removed;
    }

    /// <summary>Makes a folder the primary. It moves to the front; the others keep their order.</summary>
    /// <param name="rootId">The identifier of the folder.</param>
    /// <returns>True when the folder exists.</returns>
    public bool MakePrimary(string rootId)
    {
        ArgumentNullException.ThrowIfNull(rootId);
        var roots = Snapshot().Select(root => root with { IsPrimary = string.Equals(root.Id, rootId, StringComparison.Ordinal) }).ToList();
        if (!roots.Exists(root => root.IsPrimary))
        {
            return false;
        }

        Apply(WithPrimaryFirst(roots));
        return true;
    }

    /// <summary>
    /// Moves a folder up (negative) or down (positive) among the folders that are not the primary. The primary stays first and cannot be moved;
    /// to put a folder first, make it the primary.
    /// </summary>
    /// <param name="rootId">The identifier of the folder.</param>
    /// <param name="offset">How many places to move; the result is clamped to the non-primary folders.</param>
    /// <returns>True when the folder moved.</returns>
    public bool Move(string rootId, int offset)
    {
        ArgumentNullException.ThrowIfNull(rootId);
        var roots = Snapshot().ToList();
        var index = roots.FindIndex(root => string.Equals(root.Id, rootId, StringComparison.Ordinal));
        if (index < 1 || roots.Count < 3)
        {
            return false;
        }

        var target = Math.Clamp(index + offset, 1, roots.Count - 1);
        if (target == index)
        {
            return false;
        }

        var moving = roots[index];
        roots.RemoveAt(index);
        roots.Insert(target, moving);
        Apply(roots);
        return true;
    }

    /// <summary>Renames a folder. The identifier does not change.</summary>
    /// <param name="rootId">The identifier of the folder.</param>
    /// <param name="displayName">The new name.</param>
    /// <returns>True when the folder exists.</returns>
    /// <exception cref="ArgumentException">The name is blank.</exception>
    public bool Rename(string rootId, string displayName)
    {
        ArgumentNullException.ThrowIfNull(rootId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var roots = Snapshot().ToList();
        var index = roots.FindIndex(root => string.Equals(root.Id, rootId, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        roots[index] = roots[index] with { DisplayName = displayName.Trim() };
        Apply(roots);
        return true;
    }

    /// <summary>
    /// Replaces the folders with the ones in a <c>.code-workspace</c> file. A file that cannot be read leaves the folders as they were and sets <see cref="LastError"/>.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <returns>True when the file was imported.</returns>
    public bool ImportCodeWorkspace(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        WorkspacePickerImport import;
        try
        {
            import = _backend.ImportCodeWorkspace(filePath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            LastError = $"Could not import '{filePath}': {exception.Message}";
            return false;
        }

        Name = import.Name;
        Apply(WithPrimaryFirst(import.Roots.ToList()));
        ImportWarnings = import.Warnings;
        LastError = null;
        return true;
    }

    /// <summary>
    /// Writes the folders to a <c>.code-workspace</c> file. A failure sets <see cref="LastError"/> and writes nothing.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <returns>True when the file was written.</returns>
    public bool ExportCodeWorkspace(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        try
        {
            _backend.ExportCodeWorkspace(filePath, Snapshot());
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            LastError = $"Could not export '{filePath}': {exception.Message}";
            return false;
        }

        LastError = null;
        return true;
    }

    private static List<WorkspacePickerRoot> WithPrimaryFirst(List<WorkspacePickerRoot> roots)
    {
        if (roots.Count == 0)
        {
            return roots;
        }

        var primary = roots.FindIndex(root => root.IsPrimary);
        if (primary < 0)
        {
            primary = 0;
        }

        var result = new List<WorkspacePickerRoot>(roots.Count) { roots[primary] with { IsPrimary = true } };
        for (var index = 0; index < roots.Count; index++)
        {
            if (index != primary)
            {
                result.Add(roots[index] with { IsPrimary = false });
            }
        }

        return result;
    }

    private static string DefaultName(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        var cut = trimmed.LastIndexOfAny(['/', '\\']);
        var name = cut < 0 ? trimmed : trimmed[(cut + 1)..];
        return name.Length == 0 || name.EndsWith(':') ? FallbackName : name;
    }

    private static string Slug(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().TrimEnd('-');
        return slug.Length == 0 ? FallbackName : slug;
    }

    private static string Unique(string candidate, HashSet<string> taken)
    {
        if (!taken.Contains(candidate))
        {
            return candidate;
        }

        for (var number = 2; ; number++)
        {
            var next = candidate + "-" + number.ToString(CultureInfo.InvariantCulture);
            if (!taken.Contains(next))
            {
                return next;
            }
        }
    }

    private void Apply(List<WorkspacePickerRoot> roots)
    {
        var existing = _rows.ToDictionary(row => row.Id, StringComparer.Ordinal);
        var next = new List<WorkspacePickerRow>(roots.Count);
        foreach (var root in roots)
        {
            if (existing.TryGetValue(root.Id, out var row))
            {
                row.Update(root);
                next.Add(row);
            }
            else
            {
                next.Add(new WorkspacePickerRow(root));
            }
        }

        _rows.Clear();
        foreach (var row in next)
        {
            _rows.Add(row);
        }

        Validate();
    }

    private void Validate()
    {
        IReadOnlyList<WorkspacePickerIssue> issues = _rows.Count == 0 ? Array.Empty<WorkspacePickerIssue>() : _backend.Validate(Snapshot());
        foreach (var row in _rows)
        {
            row.SetIssues(issues.Where(issue => string.Equals(issue.RootId, row.Id, StringComparison.Ordinal)).ToArray());
        }

        _issues = issues;
        NotifyPropertyChanged(nameof(Issues));
        NotifyPropertyChanged(nameof(CanStart));
        NotifyPropertyChanged(nameof(HasWarnings));
    }
}
