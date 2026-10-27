using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Editor;

/// <summary>
/// The first level of the multi-root explorer: one root of the workspace with its own <see cref="WorkspaceTree"/>. A root that
/// could not be opened has an <see cref="Error"/> and an empty tree; the other roots are not affected.
/// </summary>
public sealed class WorkspaceRootTree : ObservableModel
{
    private readonly WorkspaceRootSource _source;
    private string? _error;
    private bool _isExpanded = true;

    internal WorkspaceRootTree(WorkspaceRootSource source)
    {
        _source = source;
        Tree = new WorkspaceTree();
    }

    /// <summary>Gets the stable identifier of the root.</summary>
    public string Id => _source.Id;

    /// <summary>Gets the root name shown on the row.</summary>
    public string DisplayName => _source.DisplayName;

    /// <summary>Gets whether this is the primary root, the working directory of the run.</summary>
    public bool IsPrimary => _source.IsPrimary;

    /// <summary>Gets the tree of the root's folder. It is empty until the root opens successfully.</summary>
    public WorkspaceTree Tree { get; }

    /// <summary>Gets why the root could not be opened, or null when it opened (or has not been tried).</summary>
    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                NotifyPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>Gets whether the root failed to open.</summary>
    public bool HasError => _error is not null;

    /// <summary>Gets or sets whether the root row shows its children.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    internal void Open()
    {
        try
        {
            Tree.OpenFolder(_source.Open());
            Error = null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            Error = $"{DisplayName}: {exception.Message}";
        }
    }
}
