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
/// The palette lists every registered command whatever its <see cref="SlashCommandDescriptor.Platforms"/>, as the registry does;
/// the host that knows its platform filters the registry it passes in.
/// </remarks>
public sealed class SlashPaletteModel : ObservableModel
{
    private readonly SlashCommandRegistry _registry;
    private bool _isOpen;
    private string _query = string.Empty;
    private IReadOnlyList<SlashPaletteEntry> _entries = [];
    private IReadOnlyList<string> _suggestions = [];
    private string? _noMatchMessage;
    private int _selectedIndex = -1;
    private string? _notice;

    /// <summary>Creates a closed palette over a command registry.</summary>
    /// <param name="registry">The commands the palette lists.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public SlashPaletteModel(SlashCommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
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
    /// Gets the registered names close to <see cref="Query"/> when no command matches it. It is empty when a command matches,
    /// when nothing is close, and while the palette is closed.
    /// </summary>
    public IReadOnlyList<string> Suggestions { get => _suggestions; private set => SetProperty(ref _suggestions, value); }

    /// <summary>
    /// Gets the text that says no command matches <see cref="Query"/>, with the suggestions when there are any, or
    /// <see langword="null"/> when a command matches or the palette is closed.
    /// </summary>
    public string? NoMatchMessage { get => _noMatchMessage; private set => SetProperty(ref _noMatchMessage, value); }

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
    /// Reports the composer draft after a change. The palette opens when the draft starts with a slash and holds no white space
    /// after it, and then lists the commands that match the text after the slash. Any other draft, including the empty draft left
    /// by a backspace over the slash, or a draft whose command name is followed by argument text, closes it.
    /// </summary>
    /// <param name="draft">The full draft text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public void UpdateDraft(string draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Notice = null;
        if (!IsCommandNameDraft(draft))
        {
            Close();
            return;
        }

        var query = draft[1..];
        var entries = _registry.MatchByPrefix(query).Select(static command => new SlashPaletteEntry(command)).ToArray();
        IReadOnlyList<string> suggestions = entries.Length == 0 ? _registry.Suggest(query) : [];

        Query = query;
        Entries = entries;
        Suggestions = suggestions;
        NoMatchMessage = entries.Length == 0 ? DescribeNoMatch(query, suggestions) : null;
        SelectedIndex = entries.Length == 0 ? -1 : 0;
        IsOpen = true;
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

    /// <summary>
    /// Accepts the selected row. An enabled command closes the palette and yields the draft text that completes it, the slash,
    /// the name and one space, ready for arguments. A disabled command keeps the palette open, yields nothing, and sets
    /// <see cref="Notice"/> to its reason.
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

    /// <summary>Closes the palette without choosing a command, as Escape does.</summary>
    public void Dismiss()
    {
        Notice = null;
        Close();
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

    private static string DescribeNoMatch(string query, IReadOnlyList<string> suggestions) =>
        suggestions.Count == 0
            ? $"No command matches '/{query}'."
            : $"No command matches '/{query}'. Did you mean {string.Join(", ", suggestions.Select(static name => "/" + name))}?";

    private void Close()
    {
        IsOpen = false;
        Query = string.Empty;
        Entries = [];
        Suggestions = [];
        NoMatchMessage = null;
        SelectedIndex = -1;
    }
}
