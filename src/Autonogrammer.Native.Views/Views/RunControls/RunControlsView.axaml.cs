using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Autonogrammer.Presentation.Approvals;

namespace Autonogrammer.Native.Views.RunControls;

/// <summary>
/// The run-controls list: one row per pending tool-approval request, each with Approve and Deny. The list is shown only while
/// a request waits, so an empty run shows nothing. Rows are built from the bound <see cref="RunControlsViewModel"/>.
/// </summary>
internal partial class RunControlsView : UserControl
{
    private RunControlsViewModel? _model;

    /// <summary>Creates the list with no data context.</summary>
    public RunControlsView()
    {
        InitializeComponent();
    }

    /// <summary>Binds the list to the run controls of the data context and rebuilds its rows.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.Approvals.CollectionChanged -= OnApprovalsChanged;
        }

        _model = DataContext as RunControlsViewModel;
        if (_model is null)
        {
            ApprovalItems.ItemsSource = null;
            ApprovalItems.ItemTemplate = null;
        }
        else
        {
            var model = _model;
            ApprovalItems.ItemTemplate = new FuncDataTemplate<PendingToolApproval>(
                (approval, _) => new ToolApprovalRowView(approval, model),
                false);
            ApprovalItems.ItemsSource = model.Approvals;
            model.Approvals.CollectionChanged += OnApprovalsChanged;
        }

        Refresh();
    }

    private void OnApprovalsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    private void Refresh() => IsVisible = _model is { Approvals.Count: > 0 };
}
