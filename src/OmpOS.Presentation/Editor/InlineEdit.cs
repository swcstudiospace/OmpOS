using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Editor;

/// <summary>
/// An inline edit on a selection (ADE-12). It captures the selected range, its text and the instruction. A proposed
/// replacement changes nothing until the user accepts it; accepting replaces exactly the captured range, and undo restores
/// the original text.
/// </summary>
/// <remarks>
/// Accept and undo first check that the buffer still holds the text they expect at the range. When the buffer changed
/// underneath the edit, they throw and change nothing, so a stale range never overwrites newer text.
/// </remarks>
public sealed class InlineEdit : ObservableModel
{
    private readonly TextDocument _document;
    private InlineEditStatus _status = InlineEditStatus.AwaitingProposal;
    private string? _proposal;

    /// <summary>Captures a selection and its instruction.</summary>
    /// <param name="document">The buffer that holds the selection.</param>
    /// <param name="start">The first character offset of the selection.</param>
    /// <param name="length">The selection length in characters. It must be positive.</param>
    /// <param name="instruction">The instruction for the edit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="instruction"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive, or the range is outside the buffer.</exception>
    /// <exception cref="ArgumentException"><paramref name="instruction"/> is empty or white space.</exception>
    public InlineEdit(TextDocument document, int start, int length, string instruction)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        _document = document;
        Start = start;
        Length = length;
        SelectedText = document.GetRange(start, length);
        Instruction = instruction;
    }

    /// <summary>Gets the first character offset of the captured selection.</summary>
    public int Start { get; }

    /// <summary>Gets the length of the captured selection in characters.</summary>
    public int Length { get; }

    /// <summary>Gets the text of the captured selection, as it was when the edit was created.</summary>
    public string SelectedText { get; }

    /// <summary>Gets the instruction that the edit sends with the selection.</summary>
    public string Instruction { get; }

    /// <summary>Gets the state of the edit.</summary>
    public InlineEditStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>Gets the proposed replacement, or null before one is proposed.</summary>
    public string? Proposal
    {
        get => _proposal;
        private set => SetProperty(ref _proposal, value);
    }

    /// <summary>Proposes a replacement for the selection. The buffer does not change. A later proposal replaces an earlier one.</summary>
    /// <param name="replacement">The proposed text for the selected range.</param>
    /// <exception cref="ArgumentNullException"><paramref name="replacement"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The edit is accepted, rejected or undone.</exception>
    public void Propose(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        RequireStatus(InlineEditStatus.AwaitingProposal, InlineEditStatus.Proposed);

        Proposal = replacement;
        Status = InlineEditStatus.Proposed;
    }

    /// <summary>Accepts the proposal: the buffer's captured range is replaced with the proposal and nothing else changes.</summary>
    /// <exception cref="InvalidOperationException">
    /// No proposal is pending, or the buffer no longer holds the captured selection at its range.
    /// </exception>
    public void Accept()
    {
        RequireStatus(InlineEditStatus.Proposed);
        if (!TextAtSelectionIs(SelectedText))
        {
            throw new InvalidOperationException("The buffer changed after the selection was captured, so the proposal no longer applies.");
        }

        _document.Replace(Start, Length, Proposal!);
        Status = InlineEditStatus.Accepted;
    }

    /// <summary>Rejects the proposal. The buffer does not change.</summary>
    /// <exception cref="InvalidOperationException">No proposal is pending.</exception>
    public void Reject()
    {
        RequireStatus(InlineEditStatus.Proposed);

        Status = InlineEditStatus.Rejected;
    }

    /// <summary>Undoes an accepted proposal: the accepted text at the range is replaced with the original selected text.</summary>
    /// <exception cref="InvalidOperationException">
    /// The edit is not accepted, or the buffer no longer holds the accepted text at the range.
    /// </exception>
    public void Undo()
    {
        RequireStatus(InlineEditStatus.Accepted);
        if (!TextAtSelectionIs(Proposal!))
        {
            throw new InvalidOperationException("The buffer changed after the edit was accepted, so undo would overwrite newer text.");
        }

        _document.Replace(Start, Proposal!.Length, SelectedText);
        Status = InlineEditStatus.Undone;
    }

    private bool TextAtSelectionIs(string expected)
    {
        var length = expected.Length;
        return Start + length <= _document.Length
            && string.Equals(_document.GetRange(Start, length), expected, StringComparison.Ordinal);
    }

    private void RequireStatus(params InlineEditStatus[] allowed)
    {
        if (!allowed.Contains(_status))
        {
            throw new InvalidOperationException($"This operation is not allowed while the inline edit is {_status}.");
        }
    }
}
