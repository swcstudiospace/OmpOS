using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Commands;

/// <summary>
/// The state behind the slash palette: whether it is open, which commands it lists for the typed name, the suggestions for an
/// unknown name, and the selected row. The composer reports every change of its draft through <see cref="UpdateDraft"/>; the
/// palette opens while the draft is a slash followed by a command name that is still being typed, and closes otherwise, so a
/// backspace over the slash closes it. The model holds no view and no key handling beyond the selection moves a host maps its
/// keys to.
/// </summary>
/// <remarks>
/// <para>
/// The palette lists the commands the engine advertises (<see cref="SetEngineCommands"/>), the OmpOS catalogue it was built over, and the custom
/// commands, skills and subagents that discovery found (<see cref="SetDiscovered"/>). A catalogue row that omp itself runs under the same name is
/// dropped for the engine's row, because the engine is the one that runs it; a Cursor built-in skill row is dropped when a skill of that name was found. Any
/// other name that two sources share is listed once per source, each with its badge, in the order the resolver tries them. The list is in ordinal name
/// order, whatever the source.
/// </para>
/// <para>
/// The palette lists every registered command whatever its <see cref="SlashCommandDescriptor.Platforms"/>, as the registry does;
/// the host that knows its platform filters the registry it passes in. It never runs a command: <see cref="Accept"/> only yields the
/// text that completes the name. The engine runs a slash command when the prompt that starts with it is sent.
/// </para>
/// </remarks>
public sealed class SlashPaletteModel : ObservableModel
{
    /// <summary>The number of rows a view shows before the list scrolls.</summary>
    public const int MaxVisibleRows = 8;

    private readonly SlashCommandRegistry _registry;
    private IReadOnlyList<SlashPaletteEntry> _engine = [];
    private SlashDiscovery _discovery = SlashDiscovery.Empty;
    private ISlashActionHost? _host;
    private IReadOnlyList<SlashPaletteEntry> _all;
    private string _draft = string.Empty;
    private string? _dismissedDraft;
    private bool _isOpen;
    private string _query = string.Empty;
    private IReadOnlyList<SlashPaletteEntry> _entries = [];
    private IReadOnlyList<string> _suggestions = [];
    private string? _noMatchMessage;
    private string? _announcement;
    private int _selectedIndex = -1;
    private string? _notice;

    /// <summary>Creates a closed palette over a command registry. It lists no engine command until <see cref="SetEngineCommands"/> is called.</summary>
    /// <param name="registry">The commands the palette lists.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public SlashPaletteModel(SlashCommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _all = Merge();
    }

    /// <summary>Gets a value indicating whether the palette is showing.</summary>
    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }

    /// <summary>Gets the command name typed after the slash. It is empty when the draft is only the slash, and while the palette is closed.</summary>
    public string Query { get => _query; private set => SetProperty(ref _query, value); }

    /// <summary>Gets the commands whose names start with <see cref="Query"/>, in ordinal name order. It is empty while the palette is closed.</summary>
    public IReadOnlyList<SlashPaletteEntry> Entries
    {
        get => _entries;
        private set
        {
            if (SetProperty(ref _entries, value))
            {
                NotifyPropertyChanged(nameof(SelectedEntry));
            }
        }
    }

    /// <summary>
    /// Gets the listed names close to <see cref="Query"/> when no command matches it. It is empty when a command matches,
    /// when nothing is close, and while the palette is closed.
    /// </summary>
    public IReadOnlyList<string> Suggestions { get => _suggestions; private set => SetProperty(ref _suggestions, value); }

    /// <summary>
    /// Gets the text that says no command matches <see cref="Query"/>, with the suggestions when there are any, or
    /// <see langword="null"/> when a command matches or the palette is closed.
    /// </summary>
    public string? NoMatchMessage { get => _noMatchMessage; private set => SetProperty(ref _noMatchMessage, value); }

    /// <summary>
    /// Gets the sentence that says how many commands the palette lists (<see cref="SlashPaletteCopy.ResultCount"/>), or <see langword="null"/> while
    /// the palette is closed. A view shows it in a live region so the result count is announced as the user types.
    /// </summary>
    public string? Announcement { get => _announcement; private set => SetProperty(ref _announcement, value); }

    /// <summary>Gets the index in <see cref="Entries"/> of the selected row, or -1 when there is none.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        private set
        {
            if (SetProperty(ref _selectedIndex, value))
            {
                NotifyPropertyChanged(nameof(SelectedEntry));
            }
        }
    }

    /// <summary>Gets the selected row, or <see langword="null"/> when there is none.</summary>
    public SlashPaletteEntry? SelectedEntry => _selectedIndex >= 0 && _selectedIndex < _entries.Count ? _entries[_selectedIndex] : null;

    /// <summary>
    /// Gets the text the palette shows after the user accepts a disabled command: that command's reason. It is cleared by the
    /// next draft change and when the palette closes.
    /// </summary>
    public string? Notice { get => _notice; private set => SetProperty(ref _notice, value); }

    /// <summary>
    /// Replaces the commands the engine advertises. Each call replaces the list as a whole; a name the engine repeats keeps its first entry and a
    /// name that is blank or holds white space is not listed. While the palette is open it lists the new commands at once and keeps the
    /// selected command selected when it is still listed.
    /// </summary>
    /// <param name="commands">The commands the engine advertises, in its order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="commands"/> or one of its entries is null.</exception>
    public void SetEngineCommands(IReadOnlyList<SlashEngineCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var engine = new List<SlashPaletteEntry>(commands.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var command in commands)
        {
            ArgumentNullException.ThrowIfNull(command, nameof(commands));
            if (IsTypableName(command.Name) && seen.Add(command.Name))
            {
                engine.Add(SlashPaletteEntry.FromEngine(command));
            }
        }

        _engine = engine;
        Refresh();
    }

    /// <summary>
    /// Replaces the custom commands, skills and subagents that discovery found. Each call replaces them as a whole. A skill whose file says it is not user-invocable is not listed. While the
    /// palette is open it lists the new rows at once and keeps the selected row selected when it is still listed.
    /// </summary>
    /// <param name="discovery">What discovery found.</param>
    /// <exception cref="ArgumentNullException"><paramref name="discovery"/> is null.</exception>
    public void SetDiscovered(SlashDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);

        _discovery = discovery;
        Refresh();
    }

    /// <summary>
    /// Sets the head that runs native actions. A catalogue row that needs an action the head cannot run is listed disabled, with a reason, instead of as a control that does nothing. With no host (the default)
    /// every native action row is disabled.
    /// </summary>
    /// <param name="host">The head's action host, or <see langword="null"/> for none.</param>
    public void SetActionHost(ISlashActionHost? host)
    {
        _host = host;
        Refresh();
    }

    /// <summary>
    /// Reports the composer draft after a change. The palette opens when the draft starts with a slash and holds no white space
    /// after it, and then lists the commands that match the text after the slash. Any other draft, including the empty draft left
    /// by a backspace over the slash, or a draft whose command name is followed by argument text, closes it. A palette that
    /// <see cref="Dismiss"/> closed stays closed until the draft changes.
    /// </summary>
    /// <param name="draft">The full draft text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public void UpdateDraft(string draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Notice = null;
        _draft = draft;
        if (_dismissedDraft is not null && !string.Equals(_dismissedDraft, draft, StringComparison.Ordinal))
        {
            _dismissedDraft = null;
        }

        Rebuild(null);
    }

    /// <summary>Moves the selection by <paramref name="delta"/> rows, wrapping past either end. It does nothing when no row is listed.</summary>
    /// <param name="delta">The number of rows to move; negative moves up.</param>
    public void MoveSelection(int delta)
    {
        if (_entries.Count == 0)
        {
            return;
        }

        var count = _entries.Count;
        var next = (((_selectedIndex + delta) % count) + count) % count;
        SelectedIndex = next;
        NotifyPropertyChanged(nameof(SelectedEntry));
    }

    /// <summary>Selects the row at <paramref name="index"/>, for example the row the user pointed at. An index outside the listed rows is ignored.</summary>
    /// <param name="index">The index in <see cref="Entries"/>.</param>
    public void Select(int index)
    {
        if (index >= 0 && index < _entries.Count)
        {
            SelectedIndex = index;
        }
    }

    /// <summary>
    /// Accepts the selected row. An enabled command closes the palette and yields the draft text that completes it, the slash,
    /// the name and one space, ready for arguments. A disabled command keeps the palette open, yields nothing, and sets
    /// <see cref="Notice"/> to its reason. Accepting runs nothing: the engine runs the command when the prompt is sent.
    /// </summary>
    /// <returns>The completed draft text, or <see langword="null"/> when nothing is selected or the command is disabled.</returns>
    public string? Accept()
    {
        if (SelectedEntry is not { } entry)
        {
            return null;
        }

        if (entry.DisabledReason is { } reason)
        {
            Notice = reason;
            return null;
        }

        Close();
        return "/" + entry.Name + " ";
    }

    /// <summary>Closes the palette without choosing a command, as Escape does. It stays closed until the draft changes.</summary>
    public void Dismiss()
    {
        Notice = null;
        _dismissedDraft = _draft;
        Close();
    }

    private static bool IsTypableName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCommandNameDraft(string draft)
    {
        if (draft.Length == 0 || draft[0] != '/')
        {
            return false;
        }

        for (var index = 1; index < draft.Length; index++)
        {
            if (char.IsWhiteSpace(draft[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void Refresh()
    {
        _all = Merge();
        if (_isOpen)
        {
            Rebuild(_selectedIndex >= 0 && _selectedIndex < _entries.Count ? _entries[_selectedIndex].Name : null);
        }
    }

    private SlashPaletteEntry[] Merge()
    {
        var engineNames = new HashSet<string>(_engine.Select(static entry => entry.Name), StringComparer.Ordinal);
        var skills = _discovery.Skills.Where(static skill => skill.UserInvocable).ToArray();
        var skillNames = new HashSet<string>(skills.Select(static skill => skill.Name), StringComparer.Ordinal);
        foreach (var name in engineNames.Where(static name => name.StartsWith(SlashPromptComposer.SkillCommandPrefix, StringComparison.Ordinal)))
        {
            skillNames.Add(name[SlashPromptComposer.SkillCommandPrefix.Length..]);
        }

        return
        [
            .. _engine
                .Concat(_registry.ListAll().Select(command => CatalogueRow(command, engineNames, skillNames)).OfType<SlashPaletteEntry>())
                .Concat(_discovery.Commands.Select(SlashPaletteEntry.FromDiscovered))
                .Concat(skills.Select(SlashPaletteEntry.FromDiscovered))
                .Concat(_discovery.Subagents.Select(SlashPaletteEntry.FromDiscovered))
                .OrderBy(static entry => entry.Name, StringComparer.Ordinal)
                .ThenBy(static entry => entry.PrecedenceRank),
        ];
    }

    /// <summary>Builds the palette row of a catalogue command, or null when another source's row stands for it.</summary>
    private SlashPaletteEntry? CatalogueRow(SlashCommandDescriptor command, HashSet<string> engineNames, HashSet<string> skillNames)
    {
        var row = SlashPaletteEntry.FromCatalogue(command);
        switch (command.Kind)
        {
            case SlashHandlerKind.OmpDelegated when engineNames.Contains(command.Name):
                return null;
            case SlashHandlerKind.Skill when skillNames.Contains(command.Name):
                return null;
            case SlashHandlerKind.Skill when command.DisabledReason is null:
                return row with { Source = SlashPaletteSource.Cursor, DisabledReason = SlashPaletteCopy.NoSkillInstalled(command.Name) };
            case SlashHandlerKind.NativeAction or SlashHandlerKind.CloudHandoff when command.DisabledReason is null && _host?.CanRun(command.HandlerId) != true:
                return row with { DisabledReason = SlashPaletteCopy.ActionNotInThisWindow };
            default:
                return row;
        }
    }

    private static string DescribeNoMatch(string query, IReadOnlyList<string> suggestions) =>
        suggestions.Count == 0
            ? $"No command matches '/{query}'."
            : $"No command matches '/{query}'. Did you mean {string.Join(", ", suggestions.Select(static name => "/" + name))}?";

    /// <summary>Lists what the last reported draft asks for, or closes the palette when that draft is not a command name being typed or was dismissed.</summary>
    /// <param name="keepSelected">The name of the row to keep selected when it is still listed, or null to select the first row.</param>
    private void Rebuild(string? keepSelected)
    {
        if (!IsCommandNameDraft(_draft) || (_dismissedDraft is not null && string.Equals(_dismissedDraft, _draft, StringComparison.Ordinal)))
        {
            Close();
            return;
        }

        var query = _draft[1..];
        var entries = _all.Where(entry => entry.Name.StartsWith(query, StringComparison.Ordinal)).ToArray();
        IReadOnlyList<string> suggestions = entries.Length == 0 ? SlashCommandRegistry.SuggestAmong(_all.Select(static entry => entry.Name), query) : [];

        var selected = 0;
        if (keepSelected is not null)
        {
            var kept = Array.FindIndex(entries, entry => string.Equals(entry.Name, keepSelected, StringComparison.Ordinal));
            selected = kept >= 0 ? kept : 0;
        }

        Query = query;
        Entries = entries;
        Suggestions = suggestions;
        NoMatchMessage = entries.Length == 0 ? DescribeNoMatch(query, suggestions) : null;
        Announcement = SlashPaletteCopy.ResultCount(entries.Length);
        SelectedIndex = entries.Length == 0 ? -1 : selected;
        IsOpen = true;
    }

    private void Close()
    {
        IsOpen = false;
        Query = string.Empty;
        Entries = [];
        Suggestions = [];
        NoMatchMessage = null;
        Announcement = null;
        SelectedIndex = -1;
    }
}
