namespace OmpOS.Presentation.Settings;

/// <summary>
/// The Keyboard page: the shortcuts the shell handles, grouped by scope, as a read-only list. Remapping is left out on purpose, so the
/// page has nothing to edit and the page says so in its description.
/// </summary>
public sealed class KeyboardSettingsPage : SettingsPageBase
{
    private readonly IKeyboardSettingsSource _source;
    private List<ShortcutGroup> _groups = [];

    /// <summary>Initializes a new instance of the <see cref="KeyboardSettingsPage"/> class.</summary>
    /// <param name="source">Where the shortcuts are read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public KeyboardSettingsPage(IKeyboardSettingsSource source)
        : base(SettingsSection.Keyboard, KeyboardCopy.Title, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the shortcuts grouped by scope: the known scopes in their fixed order, then any other scope in the order it was reported. A scope with no shortcut has no group.</summary>
    public IReadOnlyList<ShortcutGroup> Groups => _groups;

    /// <summary>Gets a value indicating whether the page loaded and the source reported no shortcut.</summary>
    public bool IsEmpty => State == SettingsPageState.Ready && _groups.Count == 0;

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(LoadCoreAsync, cancellationToken);

    /// <inheritdoc/>
    protected override void OnStateChanged() => NotifyPropertyChanged(nameof(IsEmpty));

    private static List<ShortcutGroup> Group(IReadOnlyList<ShortcutRow> rows)
    {
        var scopes = new List<string>(KeyboardCopy.ScopeOrder);
        foreach (var row in rows)
        {
            if (!scopes.Contains(row.Scope, StringComparer.Ordinal))
            {
                scopes.Add(row.Scope);
            }
        }

        var groups = new List<ShortcutGroup>();
        foreach (var scope in scopes)
        {
            var inScope = rows.Where(row => string.Equals(row.Scope, scope, StringComparison.Ordinal)).ToArray();
            if (inScope.Length > 0)
            {
                groups.Add(new ShortcutGroup(scope, inScope));
            }
        }

        return groups;
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        // The continuation must run on the context that owns the model, so the context is captured.
        var snapshot = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        _groups = Group([.. snapshot.Rows.Where(static row => row is not null)]);
        NotifyPropertyChanged(nameof(Groups));
        NotifyPropertyChanged(nameof(IsEmpty));
    }
}
