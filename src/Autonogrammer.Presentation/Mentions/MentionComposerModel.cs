using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Mentions;

/// <summary>
/// The mention state of one composer: the picker, the chips of the accepted mentions and the context estimate. The composer reports every change of the draft and the
/// caret; the model finds the token under the caret, drives the picker, and drops a chip when the user deletes its text from the draft. Accepting a row replaces
/// the typed token with the mention's canonical text and adds a chip; removing a chip removes that text. At send time the content of each chip is read.
/// </summary>
public sealed class MentionComposerModel : ObservableModel, IDisposable
{
    private readonly int _maxContentBytes;
    private IReadOnlyList<MentionChip> _chips = [];
    private MentionBudget _budget = MentionBudget.None;
    private string? _notice;
    private MentionToken? _active;

    /// <summary>Creates a model over the sources of a session.</summary>
    /// <param name="sources">The sources the picker searches.</param>
    /// <param name="maxContentBytes">The byte cap of each mention's content at send time. Defaults to <see cref="MentionLimits.DefaultContentBytes"/>.</param>
    /// <param name="timeProvider">The clock of the picker's per-source timeout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxContentBytes"/> is not positive.</exception>
    public MentionComposerModel(IReadOnlyList<IMentionSource> sources, int maxContentBytes = MentionLimits.DefaultContentBytes, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxContentBytes);

        _maxContentBytes = maxContentBytes;
        Picker = new MentionPickerModel(sources, timeProvider);
    }

    /// <summary>Gets the picker.</summary>
    public MentionPickerModel Picker { get; }

    /// <summary>Gets the chips of the attached mentions, in the order they were attached.</summary>
    public IReadOnlyList<MentionChip> Chips
    {
        get => _chips;
        private set
        {
            if (SetProperty(ref _chips, value))
            {
                NotifyPropertyChanged(nameof(HasChips));
                Budget = MentionBudgetEstimator.Estimate(value.Select(static chip => chip.Mention).ToList(), _maxContentBytes);
            }
        }
    }

    /// <summary>Gets whether any mention is attached.</summary>
    public bool HasChips => _chips.Count > 0;

    /// <summary>Gets the estimate of the context the attached mentions add.</summary>
    public MentionBudget Budget { get => _budget; private set => SetProperty(ref _budget, value); }

    /// <summary>Gets a sentence about the last send or accept that could not use a mention, or <see langword="null"/>. The next draft change clears it.</summary>
    public string? Notice { get => _notice; private set => SetProperty(ref _notice, value); }

    /// <summary>Gets the byte cap of each mention's content.</summary>
    public int MaxContentBytes => _maxContentBytes;

    /// <summary>Reports the draft after a change or a caret move.</summary>
    /// <param name="draft">The full draft.</param>
    /// <param name="caret">The caret index.</param>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public void UpdateDraft(string draft, int caret)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Notice = null;
        PruneChips(draft);
        _active = MentionParser.FindActive(draft, caret);
        Picker.Update(_active);
    }

    /// <summary>
    /// Attaches the picker's selected row: reads nothing yet, but resolves the row to a mention, adds its chip and replaces the typed token in the draft with the mention's text.
    /// </summary>
    /// <param name="draft">The draft the token was typed in.</param>
    /// <param name="cancellationToken">Cancels the resolve.</param>
    /// <returns>The edit to apply to the draft, or <see langword="null"/> when nothing was attached (a confirmation is pending, the row is disabled, the item cannot be attached, or the limit is reached).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public async Task<MentionEdit?> AcceptSelectedAsync(string draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (_active is not { } token || Picker.Accept() is not { } candidate)
        {
            return null;
        }

        var duplicate = _chips.Any(chip => chip.Mention.Kind == candidate.Kind && string.Equals(chip.Mention.Id, candidate.Id, StringComparison.Ordinal));
        if (!duplicate && _chips.Count >= MentionLimits.MaxMentionsPerMessage)
        {
            Picker.ShowFailure(MentionCopy.TooManyMentions(MentionLimits.MaxMentionsPerMessage));
            return null;
        }

        ResolvedMention mention;
        try
        {
            var source = Picker.SourceFor(candidate.Kind);
            if (source is null)
            {
                Picker.ShowFailure(MentionCopy.CannotAttach);
                return null;
            }

            mention = await source.ResolveAsync(candidate, cancellationToken).ConfigureAwait(true);
        }
        catch (MentionUnavailableException exception)
        {
            Picker.ShowFailure(exception.Message);
            return null;
        }

        var chips = _chips
            .Where(chip => !(chip.Mention.Kind == mention.Kind && string.Equals(chip.Mention.Id, mention.Id, StringComparison.Ordinal)))
            .Append(MentionChip.From(mention))
            .ToList();
        Chips = chips;

        var start = Math.Clamp(token.Start, 0, draft.Length);
        var end = Math.Clamp(token.End, start, draft.Length);
        var inserted = mention.Token + " ";
        var tail = end < draft.Length && char.IsWhiteSpace(draft[end]) ? draft[(end + 1)..] : draft[end..];
        var text = draft[..start] + inserted + tail;
        return new MentionEdit(text, start + inserted.Length);
    }

    /// <summary>Removes a chip and the text of its mention from the draft.</summary>
    /// <param name="chip">The chip to remove.</param>
    /// <param name="draft">The current draft.</param>
    /// <returns>The edit to apply to the draft; the draft is unchanged when it holds no text of the mention.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public MentionEdit RemoveChip(MentionChip chip, string draft)
    {
        ArgumentNullException.ThrowIfNull(chip);
        ArgumentNullException.ThrowIfNull(draft);

        Chips = _chips.Where(existing => !ReferenceEquals(existing, chip) && !SameMention(existing, chip)).ToList();
        var token = MentionParser.Parse(draft).FirstOrDefault(found => string.Equals(draft[found.Start..found.End], chip.Token, StringComparison.Ordinal));
        if (token is null)
        {
            return new MentionEdit(draft, draft.Length);
        }

        var end = token.End < draft.Length && char.IsWhiteSpace(draft[token.End]) ? token.End + 1 : token.End;
        return new MentionEdit(draft[..token.Start] + draft[end..], token.Start);
    }

    /// <summary>Reads the content of every chip for a send. A chip that can no longer be read is dropped from the chips and named in the result.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The attachments and the names of the chips that failed.</returns>
    public async Task<MentionReadResult> CollectAsync(CancellationToken cancellationToken)
    {
        var attachments = new List<MentionAttachment>();
        var failures = new List<string>();
        foreach (var chip in _chips)
        {
            try
            {
                var content = await chip.Mention.ReadAsync(_maxContentBytes, cancellationToken).ConfigureAwait(true);
                attachments.Add(new MentionAttachment(chip.Mention, content));
            }
            catch (MentionUnavailableException)
            {
                failures.Add(chip.Mention.Display);
            }
        }

        if (failures.Count > 0)
        {
            Chips = _chips.Where(chip => !failures.Contains(chip.Mention.Display, StringComparer.Ordinal)).ToList();
            Notice = MentionCopy.AttachFailed(failures);
        }

        return new MentionReadResult(attachments, failures);
    }

    /// <summary>Removes every chip. The composer calls it after a send was accepted.</summary>
    public void Clear() => Chips = [];

    /// <inheritdoc/>
    public void Dispose() => Picker.Dispose();

    private static bool SameMention(MentionChip left, MentionChip right) =>
        left.Mention.Kind == right.Mention.Kind && string.Equals(left.Mention.Id, right.Mention.Id, StringComparison.Ordinal);

    private void PruneChips(string draft)
    {
        if (_chips.Count == 0)
        {
            return;
        }

        var present = MentionParser.Parse(draft).Select(token => draft[token.Start..token.End]).ToHashSet(StringComparer.Ordinal);
        var kept = _chips.Where(chip => present.Contains(chip.Token)).ToList();
        if (kept.Count != _chips.Count)
        {
            Chips = kept;
        }
    }
}
