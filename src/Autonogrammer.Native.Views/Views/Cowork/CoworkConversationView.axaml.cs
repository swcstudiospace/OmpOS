using System.ComponentModel;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.State;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>
/// The Cowork conversation: transcript, composer, then approvals. Image blocks go through <see cref="ViewMapping.CreateBlock"/>,
/// which draws them with the image block view. The transcript and the approvals follow their models as they change.
/// </summary>
public partial class CoworkConversationView : UserControl
{
    /// <summary>The class set when motion is reduced.</summary>
    public const string ReducedMotionClass = "reduced-motion";

    private CoworkConversationModel? _model;
    private PropertyChangedEventHandler? _sessionChanged;
    private PropertyChangedEventHandler? _approvalsChanged;
    private IReadOnlyList<UiBlock>? _renderedBlocks;

    /// <summary>Creates the view.</summary>
    public CoworkConversationView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(DataContext as CoworkConversationModel);
    }

    /// <summary>Gets the transcript region.</summary>
    public Border TranscriptRegion => Transcript;

    /// <summary>Gets the composer view.</summary>
    public CoworkComposerView ComposerView => Composer;

    /// <summary>Gets the approvals region.</summary>
    public StackPanel ApprovalsRegion => Approvals;

    private void Bind(CoworkConversationModel? model)
    {
        Unsubscribe();
        _model = model;
        _renderedBlocks = null;
        Classes.Set(ReducedMotionClass, model?.ReducedMotion == true);
        if (model is null)
        {
            TranscriptBlocks.Children.Clear();
            Approvals.Children.Clear();
            Composer.DataContext = null;
            return;
        }

        Composer.DataContext = model.ComposerSurface;
        _sessionChanged = OnSessionChanged;
        _approvalsChanged = OnApprovalsChanged;
        model.Session.PropertyChanged += _sessionChanged;
        model.Approvals.PropertyChanged += _approvalsChanged;
        RenderTranscript();
        RenderApprovals();
    }

    private void Unsubscribe()
    {
        if (_model is null)
        {
            return;
        }

        if (_sessionChanged is not null)
        {
            _model.Session.PropertyChanged -= _sessionChanged;
        }

        if (_approvalsChanged is not null)
        {
            _model.Approvals.PropertyChanged -= _approvalsChanged;
        }
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SessionViewModel.Tree), StringComparison.Ordinal))
        {
            CoworkUiThread.Run(RenderTranscript);
        }
    }

    private void OnApprovalsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(CoworkApprovalQueue.Cards), StringComparison.Ordinal))
        {
            CoworkUiThread.Run(RenderApprovals);
        }
    }

    /// <summary>Rebuilds the transcript when the block list changed. Streamed text that does not change the blocks does not rebuild it.</summary>
    private void RenderTranscript()
    {
        if (_model is null)
        {
            return;
        }

        var blocks = _model.Session.Tree.Blocks;
        if (ReferenceEquals(blocks, _renderedBlocks))
        {
            return;
        }

        _renderedBlocks = blocks;
        TranscriptBlocks.Children.Clear();
        foreach (var block in blocks)
        {
            TranscriptBlocks.Children.Add(ViewMapping.CreateBlock(block));
        }
    }

    private void RenderApprovals()
    {
        if (_model is null)
        {
            return;
        }

        Approvals.Children.Clear();
        foreach (var card in _model.Approvals.Cards)
        {
            Approvals.Children.Add(new CoworkApprovalCard { DataContext = card });
        }
    }
}
