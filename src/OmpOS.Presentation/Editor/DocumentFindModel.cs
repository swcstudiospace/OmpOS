using System.ComponentModel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Editor;

/// <summary>
/// Find over one <see cref="TextDocument"/> (ADE-09): the query, its non-overlapping matches in document order, and the selected
/// match. It follows the document, so matches stay current while the text changes.
/// </summary>
/// <remarks>
/// Typing a query selects the first match. <see cref="Next"/> and <see cref="Previous"/> wrap: past the last match
/// <see cref="Next"/> selects the first, and before the first <see cref="Previous"/> selects the last. <see cref="SelectedIndex"/>
/// is -1 exactly when there are no matches, and an empty query has no matches. After an edit the selection keeps its index,
/// clamped to the new match count.
/// </remarks>
public sealed class DocumentFindModel : ObservableModel, IDisposable
{
    private readonly TextDocument _document;
    private string _query = string.Empty;
    private bool _caseSensitive;
    private IReadOnlyList<TextMatch> _matches = [];
    private int _selectedIndex = -1;

    /// <summary>Creates a find model over <paramref name="document"/>. The model unsubscribes when disposed.</summary>
    /// <param name="document">The document to search.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    public DocumentFindModel(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _document = document;
        _document.PropertyChanged += OnDocumentPropertyChanged;
    }

    /// <summary>Gets or sets the query. Changing it recomputes the matches and selects the first one.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string Query
    {
        get => _query;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _query, value))
            {
                Recompute(resetSelection: true);
            }
        }
    }

    /// <summary>Gets or sets whether matching respects case. Changing it recomputes the matches and selects the first one.</summary>
    public bool CaseSensitive
    {
        get => _caseSensitive;
        set
        {
            if (SetProperty(ref _caseSensitive, value))
            {
                Recompute(resetSelection: true);
            }
        }
    }

    /// <summary>Gets the matches in document order.</summary>
    public IReadOnlyList<TextMatch> Matches => _matches;

    /// <summary>Gets the number of matches.</summary>
    public int MatchCount => _matches.Count;

    /// <summary>Gets the index of the selected match, or -1 when there are no matches.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Gets the selected match, or null when there are no matches.</summary>
    public TextMatch? SelectedMatch => _selectedIndex < 0 ? null : _matches[_selectedIndex];

    /// <summary>Selects the next match, wrapping from the last to the first. Does nothing when there are no matches.</summary>
    public void Next()
    {
        if (_matches.Count == 0)
        {
            return;
        }

        SetSelectedIndex((_selectedIndex + 1) % _matches.Count);
    }

    /// <summary>Selects the previous match, wrapping from the first to the last. Does nothing when there are no matches.</summary>
    public void Previous()
    {
        if (_matches.Count == 0)
        {
            return;
        }

        SetSelectedIndex(_selectedIndex <= 0 ? _matches.Count - 1 : _selectedIndex - 1);
    }

    /// <summary>Unsubscribes from the document.</summary>
    public void Dispose()
    {
        _document.PropertyChanged -= OnDocumentPropertyChanged;
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(TextDocument.Text), StringComparison.Ordinal))
        {
            Recompute(resetSelection: false);
        }
    }

    private void Recompute(bool resetSelection)
    {
        var matches = FindAll(_document, _query, _caseSensitive);
        SetProperty<IReadOnlyList<TextMatch>>(ref _matches, matches, nameof(Matches));
        NotifyPropertyChanged(nameof(MatchCount));

        var index = matches.Count == 0
            ? -1
            : resetSelection ? 0 : Math.Clamp(_selectedIndex, 0, matches.Count - 1);
        SetSelectedIndex(index);
    }

    private void SetSelectedIndex(int index)
    {
        if (SetProperty(ref _selectedIndex, index, nameof(SelectedIndex)))
        {
            NotifyPropertyChanged(nameof(SelectedMatch));
        }
    }

    private static List<TextMatch> FindAll(TextDocument document, string query, bool caseSensitive)
    {
        if (query.Length == 0)
        {
            return [];
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var text = document.Text;
        var matches = new List<TextMatch>();
        var from = 0;
        while (from <= text.Length - query.Length)
        {
            var index = text.IndexOf(query, from, comparison);
            if (index < 0)
            {
                break;
            }

            var position = document.GetPosition(index);
            matches.Add(new TextMatch(position.Line, position.Column, query.Length));
            from = index + query.Length;
        }

        return matches;
    }
}
