using System.Collections.Specialized;
using System.ComponentModel;
using Autonogrammer.Presentation.Editor;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views.Explorer;

/// <summary>
/// The workspace file explorer. It draws the visible rows of the bound <see cref="WorkspaceTree"/>, and it maps the keyboard
/// onto the tree's keyboard model: arrows and Home and End move the selection, Right and Left expand and collapse, and Enter or
/// Space opens a file or toggles a directory. Every change to the tree, whether made here or by the orchestrator, redraws the rows.
/// </summary>
/// <remarks>
/// Model changes that arrive together (a directory's children, then its expansion) redraw once, on the next dispatcher pass.
/// Keys redraw at once. <see cref="OpenFolderRoot"/> exposes the open folder's root path for the orchestrator. The explorer
/// runs nothing: opening a file raises <see cref="FileOpenRequested"/> and the orchestrator decides what follows.
/// </remarks>
internal partial class WorkspaceTreeView : UserControl
{
    private readonly Dictionary<WorkspaceNode, NodeWatch> _watches = [];
    private WorkspaceTree? _tree;
    private WorkspaceRow[] _rows = [];
    private bool _rebuildQueued;
    private bool _syncingSelection;

    /// <summary>Creates the explorer with no tree bound.</summary>
    public WorkspaceTreeView()
    {
        InitializeComponent();

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        TreeRows.SelectionChanged += OnRowSelectionChanged;
        TreeRows.ContainerPrepared += OnContainerPrepared;
    }

    /// <summary>Raised when the user activates a file. The explorer does not open it.</summary>
    public event EventHandler<WorkspaceFileRequestedEventArgs>? FileOpenRequested;

    /// <summary>Gets the absolute path of the open folder, or null when no folder is open.</summary>
    public string? OpenFolderRoot => _tree?.RootPath;

    /// <summary>Binds the view to the tree in the data context and draws its current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        Unbind();
        var tree = DataContext as WorkspaceTree;
        _tree = tree;
        if (tree is not null)
        {
            tree.PropertyChanged += OnTreePropertyChanged;
            ((INotifyCollectionChanged)tree.Nodes).CollectionChanged += OnRootsChanged;
            WatchRoots(tree);
        }

        Rebuild();
    }

    // The row's container is the tab stop, so it carries the node's name, as the text inside it does.
    private static void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is ListBoxItem { Content: WorkspaceRow row } item)
        {
            AutomationProperties.SetName(item, row.Name);
        }
    }

    private void Unbind()
    {
        if (_tree is null)
        {
            return;
        }

        _tree.PropertyChanged -= OnTreePropertyChanged;
        ((INotifyCollectionChanged)_tree.Nodes).CollectionChanged -= OnRootsChanged;
        UnwatchAll();
        _tree = null;
    }

    private void OnTreePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WorkspaceTree.Selected), StringComparison.Ordinal))
        {
            SyncSelection();
        }
        else if (string.Equals(e.PropertyName, nameof(WorkspaceTree.RootPath), StringComparison.Ordinal))
        {
            RequestRebuild();
        }
    }

    private void OnRootsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UnwatchAll();
        if (_tree is not null)
        {
            WatchRoots(_tree);
        }

        RequestRebuild();
    }

    // Watches every node of the tree, so an expansion or a loaded child set redraws the rows.
    private void WatchRoots(WorkspaceTree tree)
    {
        foreach (var root in tree.Nodes)
        {
            Watch(root);
        }
    }

    private void Watch(WorkspaceNode node)
    {
        if (_watches.ContainsKey(node))
        {
            return;
        }

        PropertyChangedEventHandler onNodeChanged = (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(WorkspaceNode.IsExpanded), StringComparison.Ordinal))
            {
                RequestRebuild();
            }
        };
        NotifyCollectionChangedEventHandler onChildrenChanged = (_, _) =>
        {
            foreach (var child in node.Children)
            {
                Watch(child);
            }

            RequestRebuild();
        };

        node.PropertyChanged += onNodeChanged;
        ((INotifyCollectionChanged)node.Children).CollectionChanged += onChildrenChanged;
        _watches.Add(node, new NodeWatch(onNodeChanged, onChildrenChanged));

        foreach (var child in node.Children)
        {
            Watch(child);
        }
    }

    private void UnwatchAll()
    {
        foreach (var (node, watch) in _watches)
        {
            node.PropertyChanged -= watch.OnNodeChanged;
            ((INotifyCollectionChanged)node.Children).CollectionChanged -= watch.OnChildrenChanged;
        }

        _watches.Clear();
    }

    // Coalesces the model changes that arrive together into one redraw on the next dispatcher pass.
    private void RequestRebuild()
    {
        if (_rebuildQueued)
        {
            return;
        }

        _rebuildQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildQueued = false;
            Rebuild();
        });
    }

    // Redraws the visible rows and the root label. The keyboard focus stays on the list across the redraw.
    private void Rebuild()
    {
        var hadFocus = TreeRows.IsKeyboardFocusWithin;
        if (_tree is null)
        {
            _rows = [];
            RootText.Text = "No folder open";
        }
        else
        {
            _rows = _tree.VisibleNodes().Select(node => new WorkspaceRow(node)).ToArray();
            RootText.Text = _tree.RootPath ?? "No folder open";
        }

        _syncingSelection = true;
        try
        {
            TreeRows.ItemsSource = _rows;
        }
        finally
        {
            _syncingSelection = false;
        }

        SyncSelection();
        if (hadFocus)
        {
            TreeRows.Focus();
        }
    }

    // Shows the tree's selected row as the list's selection. A list selection that the tree did not make is not forwarded.
    private void SyncSelection()
    {
        var selected = _tree?.Selected;
        _syncingSelection = true;
        try
        {
            TreeRows.SelectedItem = selected is null ? null : _rows.FirstOrDefault(row => ReferenceEquals(row.Node, selected));
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void OnRowSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || _tree is null || TreeRows.SelectedItem is not WorkspaceRow row)
        {
            return;
        }

        if (!ReferenceEquals(row.Node, _tree.Selected))
        {
            _tree.Select(row.Node);
        }
    }

    // Keys reach the tree's keyboard model before the list handles them, so the list never moves its own selection.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_tree is null || e.KeyModifiers != KeyModifiers.None || !TryMapKey(e.Key, out var key))
        {
            return;
        }

        e.Handled = true;
        var file = _tree.HandleKey(key);
        Rebuild();
        if (file is not null)
        {
            FileOpenRequested?.Invoke(this, new WorkspaceFileRequestedEventArgs(file));
        }
    }

    private static bool TryMapKey(Key key, out WorkspaceKey mapped)
    {
        switch (key)
        {
            case Key.Up:
                mapped = WorkspaceKey.Up;
                return true;
            case Key.Down:
                mapped = WorkspaceKey.Down;
                return true;
            case Key.Left:
                mapped = WorkspaceKey.Left;
                return true;
            case Key.Right:
                mapped = WorkspaceKey.Right;
                return true;
            case Key.Home:
                mapped = WorkspaceKey.Home;
                return true;
            case Key.End:
                mapped = WorkspaceKey.End;
                return true;
            case Key.Enter:
            case Key.Space:
                mapped = WorkspaceKey.Enter;
                return true;
            default:
                mapped = default;
                return false;
        }
    }

    private sealed record NodeWatch(PropertyChangedEventHandler OnNodeChanged, NotifyCollectionChangedEventHandler OnChildrenChanged);
}
