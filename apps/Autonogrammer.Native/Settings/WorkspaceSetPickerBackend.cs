using Autonogrammer.Presentation.Editor;
using Autonogrammer.Workspace.Sets;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// The desktop implementation of <see cref="IWorkspaceSetPickerBackend"/>: the rules of <see cref="WorkspaceSetValidator"/> over the local file system, and reading and
/// writing <c>.code-workspace</c> files with <see cref="CodeWorkspaceFile"/>. It maps the plain picker rows to <see cref="WorkspaceSetRoot"/> and the findings back.
/// </summary>
internal sealed class WorkspaceSetPickerBackend : IWorkspaceSetPickerBackend
{
    private const string CheckedSetId = "picker";
    private const string CheckedSetName = "workspace";

    private readonly WorkspaceSetValidator _validator;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceSetPickerBackend"/> class over the local file system.</summary>
    public WorkspaceSetPickerBackend()
        : this(new WorkspaceSetValidator(new LocalDirectoryProbe()))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WorkspaceSetPickerBackend"/> class over a validator.</summary>
    /// <param name="validator">Judges the sets.</param>
    /// <exception cref="ArgumentNullException"><paramref name="validator"/> is null.</exception>
    internal WorkspaceSetPickerBackend(WorkspaceSetValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
    }

    /// <summary>Maps picker rows to the roots of a set.</summary>
    /// <param name="roots">The rows, primary first.</param>
    /// <returns>The roots.</returns>
    internal static WorkspaceSetRoot[] ToSetRoots(IReadOnlyList<WorkspacePickerRoot> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        return [.. roots.Select(static root => new WorkspaceSetRoot(root.Id, root.Path, root.DisplayName, root.EnvironmentId, root.IsPrimary))];
    }

    /// <summary>Maps the roots of a set to picker rows.</summary>
    /// <param name="set">The set.</param>
    /// <returns>The rows, primary first.</returns>
    internal static WorkspacePickerRoot[] ToPickerRoots(WorkspaceSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return [.. set.Roots.Select(static root => new WorkspacePickerRoot(root.Id, root.Path, root.DisplayName, root.EnvironmentId, root.IsPrimary))];
    }

    /// <inheritdoc/>
    public IReadOnlyList<WorkspacePickerIssue> Validate(IReadOnlyList<WorkspacePickerRoot> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Count == 0)
        {
            return [];
        }

        var validation = _validator.Validate(new WorkspaceSet(CheckedSetId, CheckedSetName, ToSetRoots(roots)));
        return [.. validation.Issues.Select(static issue => new WorkspacePickerIssue(issue.Severity == WorkspaceSetIssueSeverity.Error, issue.RootId, issue.Message))];
    }

    /// <inheritdoc/>
    public WorkspacePickerImport ImportCodeWorkspace(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var import = CodeWorkspaceFile.Read(filePath).ToWorkspaceSet();
        return new WorkspacePickerImport(import.Set.Name, ToPickerRoots(import.Set), import.Warnings);
    }

    /// <inheritdoc/>
    public void ExportCodeWorkspace(string filePath, IReadOnlyList<WorkspacePickerRoot> roots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Count == 0)
        {
            throw new InvalidOperationException(WorkspacesSourceCopy.NothingToExport);
        }

        var set = new WorkspaceSet(CheckedSetId, CheckedSetName, ToSetRoots(roots));

        // An existing file keeps everything it holds besides its folders.
        var file = File.Exists(filePath) ? CodeWorkspaceFile.Read(filePath).WithSet(set) : CodeWorkspaceFile.Create(set, filePath);
        file.Write();
    }
}
