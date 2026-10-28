using System.ComponentModel;
using Avalonia.Controls;
using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Native.Views.InlineEditing;

/// <summary>
/// The inline edit on one selection. It shows the captured selection and instruction, takes a proposed replacement, and draws
/// the proposal as a reviewable change: the selected text removed and the proposal added. Accept, Reject and Undo act on the
/// bound <see cref="InlineEdit"/>; the buffer changes only through Accept and Undo.
/// </summary>
/// <remarks>
/// When the buffer changed after the selection was captured, Accept and Undo change nothing; the model's message appears on the
/// status line.
/// </remarks>
internal partial class InlineEditView : UserControl
{
    private InlineEdit? _edit;

    /// <summary>Creates the view with no edit bound.</summary>
    public InlineEditView()
    {
        InitializeComponent();

        ProposeButton.Click += (_, _) => Act(edit => edit.Propose(ProposalInput.Text ?? string.Empty));
        AcceptButton.Click += (_, _) => Act(edit => edit.Accept());
        RejectButton.Click += (_, _) => Act(edit => edit.Reject());
        UndoButton.Click += (_, _) => Act(edit => edit.Undo());
    }

    /// <summary>Binds the view to the inline edit in the data context and draws its current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        Unbind();
        _edit = DataContext as InlineEdit;
        if (_edit is not null)
        {
            _edit.PropertyChanged += OnEditChanged;
        }

        Refresh();
    }

    private void Unbind()
    {
        if (_edit is not null)
        {
            _edit.PropertyChanged -= OnEditChanged;
        }

        _edit = null;
    }

    private void OnEditChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    // Runs one model action. A refused action (the buffer moved, or the status forbids it) changes nothing and reports its message.
    private void Act(Action<InlineEdit> action)
    {
        if (_edit is null)
        {
            return;
        }

        try
        {
            action(_edit);
        }
        catch (InvalidOperationException error)
        {
            StatusText.Text = error.Message;
            return;
        }

        Refresh();
    }

    // Draws the edit from the model: the text it captured, what each control may do now, the change, and the status.
    private void Refresh()
    {
        if (_edit is null)
        {
            InstructionText.Text = string.Empty;
            SelectionText.Text = string.Empty;
            StatusText.Text = string.Empty;
            ChangePanel.IsVisible = false;
            SetControls(canPropose: false, canReview: false, canUndo: false);
            return;
        }

        InstructionText.Text = _edit.Instruction;
        SelectionText.Text = _edit.SelectedText;

        var status = _edit.Status;
        var reviewing = status is InlineEditStatus.Proposed or InlineEditStatus.Accepted;
        ChangePanel.IsVisible = reviewing;
        if (reviewing)
        {
            RemovedText.Text = Prefixed(_edit.SelectedText, "-");
            AddedText.Text = Prefixed(_edit.Proposal ?? string.Empty, "+");
        }

        SetControls(
            canPropose: status is InlineEditStatus.AwaitingProposal or InlineEditStatus.Proposed,
            canReview: status == InlineEditStatus.Proposed,
            canUndo: status == InlineEditStatus.Accepted);
        StatusText.Text = Describe(status);
    }

    private void SetControls(bool canPropose, bool canReview, bool canUndo)
    {
        ProposalInput.IsEnabled = canPropose;
        ProposeButton.IsEnabled = canPropose;
        AcceptButton.IsEnabled = canReview;
        RejectButton.IsEnabled = canReview;
        UndoButton.IsEnabled = canUndo;
    }

    private static string Prefixed(string text, string marker) =>
        string.Join('\n', text.Split('\n').Select(line => $"{marker} {line}"));

    private static string Describe(InlineEditStatus status) => status switch
    {
        InlineEditStatus.AwaitingProposal => "Enter a proposed replacement, then propose it.",
        InlineEditStatus.Proposed => "Review the proposed change, then accept or reject it.",
        InlineEditStatus.Accepted => "Accepted. Undo restores the original text.",
        InlineEditStatus.Rejected => "Rejected. The text is unchanged.",
        InlineEditStatus.Undone => "Undone. The original text is back.",
        _ => throw new InvalidOperationException($"Unknown inline edit status {status}."),
    };
}
