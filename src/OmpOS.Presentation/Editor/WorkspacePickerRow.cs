using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Editor;

/// <summary>
/// One row of the set picker: a folder, its name, whether it is the primary, and what validation says about it.
/// </summary>
public sealed class WorkspacePickerRow : ObservableModel
{
    private WorkspacePickerRoot _root;
    private IReadOnlyList<WorkspacePickerIssue> _issues = [];

    internal WorkspacePickerRow(WorkspacePickerRoot root)
    {
        _root = root;
    }

    /// <summary>Gets the identifier of the root.</summary>
    public string Id => _root.Id;

    /// <summary>Gets the folder path.</summary>
    public string Path => _root.Path;

    /// <summary>Gets the name shown to the user.</summary>
    public string DisplayName => _root.DisplayName;

    /// <summary>Gets the environment that owns the folder, or null for this machine.</summary>
    public string? EnvironmentId => _root.EnvironmentId;

    /// <summary>Gets whether the row is the primary, the working directory of the run.</summary>
    public bool IsPrimary => _root.IsPrimary;

    /// <summary>Gets the findings about this row.</summary>
    public IReadOnlyList<WorkspacePickerIssue> Issues => _issues;

    /// <summary>Gets whether a finding blocks the set.</summary>
    public bool HasError => _issues.Any(issue => issue.IsError);

    /// <summary>Gets whether the row has findings and none blocks the set.</summary>
    public bool HasWarning => _issues.Count > 0 && !HasError;

    /// <summary>Gets the findings joined into one text for a tooltip or caption; empty when there is none.</summary>
    public string IssueText => string.Join(" ", _issues.Select(issue => issue.Message));

    internal WorkspacePickerRoot Root => _root;

    internal void Update(WorkspacePickerRoot root)
    {
        if (_root == root)
        {
            return;
        }

        _root = root;
        NotifyPropertyChanged(nameof(Id));
        NotifyPropertyChanged(nameof(Path));
        NotifyPropertyChanged(nameof(DisplayName));
        NotifyPropertyChanged(nameof(EnvironmentId));
        NotifyPropertyChanged(nameof(IsPrimary));
    }

    internal void SetIssues(IReadOnlyList<WorkspacePickerIssue> issues)
    {
        _issues = issues;
        NotifyPropertyChanged(nameof(Issues));
        NotifyPropertyChanged(nameof(HasError));
        NotifyPropertyChanged(nameof(HasWarning));
        NotifyPropertyChanged(nameof(IssueText));
    }
}
