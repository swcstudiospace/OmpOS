using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Presentation.Safety;

/// <summary>
/// The Run mode page: the three modes and what each does in Autonogrammer, the allowlists and read boundary you can edit (saved to Autonogrammer-owned files under <c>.autonogrammer</c> only),
/// the files and rules in force with the winning file named, the problems found in them, and the plain statement that Auto-review's classifier is not available.
/// Edits are a draft until <see cref="EditableSettingsPage{TValues}.SaveAsync"/> writes them through the source. A rejected entry never changes the draft: it shows an inline message.
/// </summary>
public sealed class RunModeSettingsPage : EditableSettingsPage<RunModeValues>
{
    private const int MaxEntryLength = 512;

    private static readonly IReadOnlyList<RunModeOption> ModeOptions =
    [
        new(RunModeChoice.AutoReview, SafetyCopy.AutoReviewTitle, SafetyCopy.AutoReviewSummary),
        new(RunModeChoice.Allowlist, SafetyCopy.AllowlistTitle, SafetyCopy.AllowlistSummary),
        new(RunModeChoice.RunEverything, SafetyCopy.RunEverythingTitle, SafetyCopy.RunEverythingSummary),
    ];

    private readonly IRunModeSource _source;
    private RunModeSnapshot _snapshot = RunModeSnapshot.Empty;
    private string _entryError = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="RunModeSettingsPage"/> class.</summary>
    /// <param name="section">The section the page fills. The settings window gives it its own section.</param>
    /// <param name="source">Where the values are read and written.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public RunModeSettingsPage(SettingsSection section, IRunModeSource source)
        : base(section, SafetyCopy.Title, RunModeValues.Defaults)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the three modes with their sentences, in display order.</summary>
    public static IReadOnlyList<RunModeOption> Options => ModeOptions;

    /// <summary>Gets the mode in the draft.</summary>
    public RunModeChoice Mode => Draft.Mode;

    /// <summary>Gets the sentence for the mode in the draft.</summary>
    public string ModeSummary => ModeOptions.First(option => option.Choice == Draft.Mode).Summary;

    /// <summary>Gets the line that names the mode in force.</summary>
    public string EffectiveModeText => SafetyCopy.EffectiveMode(ModeOptions.First(option => option.Choice == Draft.Mode).Title);

    /// <summary>Gets a value indicating whether the Run Everything warning is showing.</summary>
    public bool IsRunEverythingWarningVisible => Draft.Mode == RunModeChoice.RunEverything;

    /// <summary>Gets a value indicating whether the classifier note is showing: it shows in Auto-review, the mode whose Cursor behaviour it explains.</summary>
    public bool IsClassifierNoteVisible => Draft.Mode == RunModeChoice.AutoReview;

    /// <summary>Gets a value indicating whether the read access section is showing: it is hidden in Run Everything, as in Cursor.</summary>
    public bool IsReadAccessVisible => Draft.Mode != RunModeChoice.RunEverything;

    /// <summary>Gets the read boundary in the draft.</summary>
    public ReadBoundaryChoice ReadBoundary => Draft.ReadBoundary;

    /// <summary>Gets the line that says which file controls the read boundary.</summary>
    public string BoundarySourceText => SafetyCopy.BoundarySource(_snapshot.ReadBoundarySource);

    /// <summary>Gets the line that says which file controls the read allowlist.</summary>
    public string ReadPathsSourceText => SafetyCopy.ReadPathsSource(_snapshot.ReadPathsSource);

    /// <summary>Gets the line that says where allowlist entries are saved.</summary>
    public string PermissionsTargetText => SafetyCopy.SavedTo(_snapshot.EditPermissionsPath);

    /// <summary>Gets the line that says where the read boundary and read paths are saved.</summary>
    public string SandboxTargetText => SafetyCopy.SavedTo(_snapshot.EditSandboxPath);

    /// <summary>Gets the terminal allowlist: the entries of other files first (read-only), then the draft's own (removable).</summary>
    public IReadOnlyList<RunModeEntry> TerminalRows => [.. _snapshot.OtherTerminalAllowlist, .. Own(Draft.TerminalAllowlist, _snapshot.EditPermissionsPath)];

    /// <summary>Gets the MCP allowlist: the entries of other files first (read-only), then the draft's own (removable).</summary>
    public IReadOnlyList<RunModeEntry> McpRows => [.. _snapshot.OtherMcpAllowlist, .. Own(Draft.McpAllowlist, _snapshot.EditPermissionsPath)];

    /// <summary>Gets the read allowlist: the one in force (read-only), then the draft's own entries (removable) when they differ from it.</summary>
    public IReadOnlyList<RunModeEntry> ReadPathRows =>
        _snapshot.ReadPathsSource is { Length: > 0 } source && string.Equals(source, _snapshot.EditSandboxPath, StringComparison.Ordinal)
            ? [.. Own(Draft.AdditionalReadPaths, _snapshot.EditSandboxPath)]
            : [.. _snapshot.EffectiveReadPaths, .. Own(Draft.AdditionalReadPaths, _snapshot.EditSandboxPath)];

    /// <summary>Gets the allow rules in force.</summary>
    public IReadOnlyList<RunModeEntry> AllowRules => _snapshot.AllowRules;

    /// <summary>Gets the deny rules in force.</summary>
    public IReadOnlyList<RunModeEntry> DenyRules => _snapshot.DenyRules;

    /// <summary>Gets the sentences that lean towards allowing. They are not enforced.</summary>
    public IReadOnlyList<RunModeEntry> AllowInstructions => _snapshot.AllowInstructions;

    /// <summary>Gets the sentences that lean towards blocking. They are not enforced.</summary>
    public IReadOnlyList<RunModeEntry> BlockInstructions => _snapshot.BlockInstructions;

    /// <summary>Gets a value indicating whether any autoRun sentence exists.</summary>
    public bool HasInstructions => _snapshot.AllowInstructions.Count + _snapshot.BlockInstructions.Count > 0;

    /// <summary>Gets the files that were looked at.</summary>
    public IReadOnlyList<RunModeFileRow> Files => _snapshot.Files;

    /// <summary>Gets the problems found in the files.</summary>
    public IReadOnlyList<RunModeDiagnostic> Diagnostics => _snapshot.Diagnostics;

    /// <summary>Gets a value indicating whether a file has a problem.</summary>
    public bool HasDiagnostics => _snapshot.Diagnostics.Count > 0;

    /// <summary>Gets a value indicating whether the workspace is not trusted.</summary>
    public bool IsProjectUntrusted => !_snapshot.ProjectTrusted;

    /// <summary>Gets the message of the latest rejected entry, or an empty string.</summary>
    public string EntryError => _entryError;

    /// <summary>Gets a value indicating whether a rejected entry's message is showing.</summary>
    public bool HasEntryError => _entryError.Length > 0;

    /// <summary>Gets a value indicating whether entries can be added: there is a file to save them to.</summary>
    public bool CanEditEntries => _snapshot.EditPermissionsPath.Length > 0;

    /// <summary>Gets a value indicating whether the read boundary and read paths can be edited: there is a file to save them to.</summary>
    public bool CanEditSandbox => _snapshot.EditSandboxPath.Length > 0;

    /// <summary>Sets the mode in the draft. Ignored while the page is not ready.</summary>
    /// <param name="mode">The mode.</param>
    public void SetMode(RunModeChoice mode) => SetDraft(Draft with { Mode = mode });

    /// <summary>Sets the read boundary in the draft. Ignored while the page is not ready or has no file to save it to.</summary>
    /// <param name="boundary">The boundary.</param>
    public void SetReadBoundary(ReadBoundaryChoice boundary)
    {
        if (CanEditSandbox)
        {
            SetDraft(Draft with { ReadBoundary = boundary });
        }
    }

    /// <summary>Adds a terminal allowlist entry to the draft.</summary>
    /// <param name="text">The entry, for example <c>git status</c>.</param>
    /// <returns>True when it was added; false when it was rejected (the page then shows why).</returns>
    public bool AddTerminalEntry(string? text) => Add(text, Draft.TerminalAllowlist, CanEditEntries, static entry => null, list => Draft with { TerminalAllowlist = list });

    /// <summary>Removes a terminal allowlist entry from the draft. Entries of other files cannot be removed here.</summary>
    /// <param name="text">The entry.</param>
    /// <returns>True when it was removed.</returns>
    public bool RemoveTerminalEntry(string text) => Remove(text, Draft.TerminalAllowlist, list => Draft with { TerminalAllowlist = list });

    /// <summary>Adds an MCP allowlist entry to the draft.</summary>
    /// <param name="text">The entry, written <c>server:tool</c>.</param>
    /// <returns>True when it was added; false when it was rejected (the page then shows why).</returns>
    public bool AddMcpEntry(string? text) => Add(text, Draft.McpAllowlist, CanEditEntries, McpProblem, list => Draft with { McpAllowlist = list });

    /// <summary>Removes an MCP allowlist entry from the draft.</summary>
    /// <param name="text">The entry.</param>
    /// <returns>True when it was removed.</returns>
    public bool RemoveMcpEntry(string text) => Remove(text, Draft.McpAllowlist, list => Draft with { McpAllowlist = list });

    /// <summary>Adds a read path to the draft.</summary>
    /// <param name="text">The path: absolute, <c>~</c>-relative, or a glob.</param>
    /// <returns>True when it was added; false when it was rejected (the page then shows why).</returns>
    public bool AddReadPath(string? text) => Add(text, Draft.AdditionalReadPaths, CanEditSandbox, ReadPathProblem, list => Draft with { AdditionalReadPaths = list });

    /// <summary>Removes a read path from the draft.</summary>
    /// <param name="text">The path.</param>
    /// <returns>True when it was removed.</returns>
    public bool RemoveReadPath(string text) => Remove(text, Draft.AdditionalReadPaths, list => Draft with { AdditionalReadPaths = list });

    /// <inheritdoc/>
    public override void Discard()
    {
        SetEntryError(string.Empty);
        base.Discard();
    }

    /// <summary>Saves the draft, then reads the files again so the winning files and the rows show what is now in force.</summary>
    /// <param name="cancellationToken">Cancels the save and the reload.</param>
    /// <returns>True when the draft was saved or there was nothing to save.</returns>
    public override async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        var saved = await base.SaveAsync(cancellationToken).ConfigureAwait(true);
        if (!saved)
        {
            return false;
        }

        try
        {
            _snapshot = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
            NotifySnapshotProperties();
            NotifyValueProperties();
        }
        catch (SettingsSourceException)
        {
            // The values were saved. The rows keep what was read before; the next load refreshes them.
        }

        return true;
    }

    /// <inheritdoc/>
    protected override async Task<RunModeValues> LoadValuesAsync(CancellationToken cancellationToken)
    {
        _snapshot = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        SetEntryError(string.Empty);
        NotifySnapshotProperties();
        return _snapshot.Values;
    }

    /// <inheritdoc/>
    protected override Task SaveValuesAsync(RunModeValues values, CancellationToken cancellationToken) => _source.SaveAsync(values, cancellationToken);

    /// <inheritdoc/>
    protected override void NotifyValueProperties()
    {
        NotifyPropertyChanged(nameof(Mode));
        NotifyPropertyChanged(nameof(ModeSummary));
        NotifyPropertyChanged(nameof(EffectiveModeText));
        NotifyPropertyChanged(nameof(IsRunEverythingWarningVisible));
        NotifyPropertyChanged(nameof(IsClassifierNoteVisible));
        NotifyPropertyChanged(nameof(IsReadAccessVisible));
        NotifyPropertyChanged(nameof(ReadBoundary));
        NotifyPropertyChanged(nameof(TerminalRows));
        NotifyPropertyChanged(nameof(McpRows));
        NotifyPropertyChanged(nameof(ReadPathRows));
    }

    private static IEnumerable<RunModeEntry> Own(IReadOnlyList<string> entries, string source) => entries.Select(entry => new RunModeEntry(entry, source, IsEditable: true));

    private static string? McpProblem(string entry)
    {
        var colon = entry.IndexOf(':', StringComparison.Ordinal);
        return colon <= 0 || colon == entry.Length - 1 ? SafetyCopy.McpEntryInvalid : null;
    }

    private static string? ReadPathProblem(string entry)
    {
        var rooted = entry[0] is '/' or '~' or '\\' || (entry.Length > 2 && char.IsLetter(entry[0]) && entry[1] == ':' && entry[2] is '/' or '\\');
        return rooted ? null : SafetyCopy.ReadPathRelative;
    }

    private bool Add(string? text, IReadOnlyList<string> current, bool canEdit, Func<string, string?> problem, Func<List<string>, RunModeValues> apply)
    {
        if (State != SettingsPageState.Ready)
        {
            return false;
        }

        var entry = text?.Trim() ?? string.Empty;
        var error = !canEdit ? SafetyCopy.NoTarget
            : entry.Length == 0 ? SafetyCopy.EntryEmpty
            : entry.Length > MaxEntryLength ? SafetyCopy.EntryTooLong
            : entry.Contains('\n', StringComparison.Ordinal) || entry.Contains('\r', StringComparison.Ordinal) ? SafetyCopy.EntryMultiline
            : current.Contains(entry, StringComparer.Ordinal) ? SafetyCopy.EntryDuplicate
            : problem(entry);
        if (error is not null)
        {
            SetEntryError(error);
            return false;
        }

        SetEntryError(string.Empty);
        return SetDraft(apply([.. current, entry]));
    }

    private bool Remove(string text, IReadOnlyList<string> current, Func<List<string>, RunModeValues> apply)
    {
        if (State != SettingsPageState.Ready || !current.Contains(text, StringComparer.Ordinal))
        {
            return false;
        }

        SetEntryError(string.Empty);
        return SetDraft(apply([.. current.Where(entry => !string.Equals(entry, text, StringComparison.Ordinal))]));
    }

    private void SetEntryError(string message)
    {
        if (SetProperty(ref _entryError, message, nameof(EntryError)))
        {
            NotifyPropertyChanged(nameof(HasEntryError));
        }
    }

    private void NotifySnapshotProperties()
    {
        NotifyPropertyChanged(nameof(BoundarySourceText));
        NotifyPropertyChanged(nameof(ReadPathsSourceText));
        NotifyPropertyChanged(nameof(PermissionsTargetText));
        NotifyPropertyChanged(nameof(SandboxTargetText));
        NotifyPropertyChanged(nameof(AllowRules));
        NotifyPropertyChanged(nameof(DenyRules));
        NotifyPropertyChanged(nameof(AllowInstructions));
        NotifyPropertyChanged(nameof(BlockInstructions));
        NotifyPropertyChanged(nameof(HasInstructions));
        NotifyPropertyChanged(nameof(Files));
        NotifyPropertyChanged(nameof(Diagnostics));
        NotifyPropertyChanged(nameof(HasDiagnostics));
        NotifyPropertyChanged(nameof(IsProjectUntrusted));
        NotifyPropertyChanged(nameof(CanEditEntries));
        NotifyPropertyChanged(nameof(CanEditSandbox));
    }
}
