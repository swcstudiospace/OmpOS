namespace Autonogrammer.Presentation.Settings.Ignore;

/// <summary>
/// The Ignore page: the global ignore patterns you edit, the two switches, and, read-only, the built-in list and the ignore files found in
/// the workspace. Everything you edit is a draft until Save writes it. A pattern that is refused is not added and the page says why.
/// </summary>
public sealed class IgnoreSettingsPage : EditableSettingsPage<IgnoreSettingsValues>
{
    private readonly IIgnoreSettingsSource _source;
    private IReadOnlyList<IgnoreFileRow> _files = [];
    private string _entryError = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="IgnoreSettingsPage"/> class.</summary>
    /// <param name="source">Where the values, the built-in list and the files are read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public IgnoreSettingsPage(IIgnoreSettingsSource source)
        : base(SettingsSection.Ignore, IgnoreCopy.Title, IgnoreSettingsValues.Defaults)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the patterns of the draft.</summary>
    public IReadOnlyList<string> Patterns => Draft.GlobalPatterns;

    /// <summary>Gets a value indicating whether the draft has no pattern.</summary>
    public bool IsEmpty => Draft.GlobalPatterns.Count == 0;

    /// <summary>Gets a value indicating whether the draft uses the built-in list.</summary>
    public bool UseDefaultList => Draft.UseDefaultList;

    /// <summary>Gets a value indicating whether the draft reads ignore files below the root.</summary>
    public bool HierarchicalIgnoreFiles => Draft.HierarchicalIgnoreFiles;

    /// <summary>Gets the built-in list. It is fixed and read-only.</summary>
    public IReadOnlyList<string> DefaultPatterns => _source.DefaultPatterns;

    /// <summary>Gets the ignore files found in the workspace at the last load.</summary>
    public IReadOnlyList<IgnoreFileRow> Files => _files;

    /// <summary>Gets why the last pattern was refused, or an empty string.</summary>
    public string EntryError => _entryError;

    /// <summary>Gets a value indicating whether the last pattern was refused.</summary>
    public bool HasEntryError => _entryError.Length > 0;

    /// <summary>Adds a pattern to the draft.</summary>
    /// <param name="text">The pattern. It is trimmed.</param>
    /// <returns>True when it was added; false when it was refused (the page then says why) or the page is not ready.</returns>
    public bool AddPattern(string? text)
    {
        if (State != SettingsPageState.Ready)
        {
            return false;
        }

        var pattern = (text ?? string.Empty).Trim();
        var problem = IgnoreSettingsLimits.PatternProblem(pattern);
        if (problem is null && Draft.GlobalPatterns.Contains(pattern, StringComparer.Ordinal))
        {
            problem = IgnoreCopy.DuplicatePattern;
        }

        if (problem is null)
        {
            problem = IgnoreSettingsLimits.ListProblem([.. Draft.GlobalPatterns, pattern]);
        }

        if (problem is not null)
        {
            SetEntryError(problem);
            return false;
        }

        SetEntryError(string.Empty);
        return SetDraft(Draft with { GlobalPatterns = [.. Draft.GlobalPatterns, pattern] });
    }

    /// <summary>Removes a pattern from the draft.</summary>
    /// <param name="pattern">The pattern, exactly as listed.</param>
    /// <returns>True when it was removed.</returns>
    public bool RemovePattern(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var index = IndexOf(Draft.GlobalPatterns, pattern);
        if (State != SettingsPageState.Ready || index < 0)
        {
            return false;
        }

        SetEntryError(string.Empty);
        return SetDraft(Draft with { GlobalPatterns = [.. Draft.GlobalPatterns.Where((_, i) => i != index)] });
    }

    /// <summary>Sets whether the draft uses the built-in list. Ignored while the page is not ready.</summary>
    /// <param name="enabled">True to use it.</param>
    public void SetUseDefaultList(bool enabled) => SetDraft(Draft with { UseDefaultList = enabled });

    /// <summary>Sets whether the draft reads ignore files below the root. Ignored while the page is not ready.</summary>
    /// <param name="enabled">True to read them.</param>
    public void SetHierarchicalIgnoreFiles(bool enabled) => SetDraft(Draft with { HierarchicalIgnoreFiles = enabled });

    /// <inheritdoc/>
    public override void Reset()
    {
        SetEntryError(string.Empty);
        base.Reset();
    }

    /// <inheritdoc/>
    public override void Discard()
    {
        SetEntryError(string.Empty);
        base.Discard();
    }

    /// <inheritdoc/>
    protected override async Task<IgnoreSettingsValues> LoadValuesAsync(CancellationToken cancellationToken)
    {
        // The continuation must run on the context that owns the model, so the context is captured.
        var values = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        _files = await _source.FoundFilesAsync(cancellationToken).ConfigureAwait(true);
        SetEntryError(string.Empty);
        NotifyPropertyChanged(nameof(Files));
        return values;
    }

    /// <inheritdoc/>
    protected override Task SaveValuesAsync(IgnoreSettingsValues values, CancellationToken cancellationToken) =>
        _source.SaveAsync(values, cancellationToken);

    /// <inheritdoc/>
    protected override void NotifyValueProperties()
    {
        NotifyPropertyChanged(nameof(Patterns));
        NotifyPropertyChanged(nameof(IsEmpty));
        NotifyPropertyChanged(nameof(UseDefaultList));
        NotifyPropertyChanged(nameof(HierarchicalIgnoreFiles));
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void SetEntryError(string message)
    {
        if (SetProperty(ref _entryError, message, nameof(EntryError)))
        {
            NotifyPropertyChanged(nameof(HasEntryError));
        }
    }
}
