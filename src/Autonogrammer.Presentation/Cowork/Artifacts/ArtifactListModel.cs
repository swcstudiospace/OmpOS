using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// The artifacts the panel lists, with the selection and the toolbar command rules. Rows keep their first-seen order, so a
/// new version of an existing artifact never reorders the list or moves the selection.
/// </summary>
public sealed class ArtifactListModel : ObservableModel
{
    private readonly List<ArtifactListItem> _items = [];
    private string? _selectedId;

    /// <summary>Gets the rows, in first-seen order.</summary>
    public IReadOnlyList<ArtifactListItem> Items => _items;

    /// <summary>Gets the selected artifact identifier, or null.</summary>
    public string? SelectedArtifactId => _selectedId;

    /// <summary>Adds a row, or replaces the row with the same artifact identifier in place.</summary>
    /// <param name="item">The row.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void Upsert(ArtifactListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = _items.FindIndex(existing => string.Equals(existing.ArtifactId, item.ArtifactId, StringComparison.Ordinal));
        if (index >= 0)
        {
            _items[index] = item;
        }
        else
        {
            _items.Add(item);
        }

        NotifyPropertyChanged(nameof(Items));
    }

    /// <summary>Selects a row.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <returns>True when the row exists and is selected.</returns>
    public bool Select(string artifactId)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        if (!_items.Exists(item => string.Equals(item.ArtifactId, artifactId, StringComparison.Ordinal)))
        {
            return false;
        }

        _selectedId = artifactId;
        NotifyPropertyChanged(nameof(SelectedArtifactId));
        return true;
    }

    /// <summary>Removes a row and clears the selection when it was selected.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    public void Remove(string artifactId)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        _items.RemoveAll(item => string.Equals(item.ArtifactId, artifactId, StringComparison.Ordinal));
        if (string.Equals(_selectedId, artifactId, StringComparison.Ordinal))
        {
            _selectedId = null;
            NotifyPropertyChanged(nameof(SelectedArtifactId));
        }

        NotifyPropertyChanged(nameof(Items));
    }

    /// <summary>Removes every row. Sign-out calls this.</summary>
    public void Clear()
    {
        _items.Clear();
        _selectedId = null;
        NotifyPropertyChanged(nameof(Items));
        NotifyPropertyChanged(nameof(SelectedArtifactId));
    }

    /// <summary>
    /// Returns the toolbar for one row. Diff needs a previous version. Copy and Edit need text, so an image has neither.
    /// Access needs a declared origin. Stop needs a running surface. Close and Export are always enabled.
    /// </summary>
    /// <param name="item">The row.</param>
    /// <param name="surfaceActive">Whether the surface is loading or running.</param>
    /// <returns>Every command, in toolbar order, with its enablement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public static IReadOnlyList<ArtifactCommandState> CommandsFor(ArtifactListItem item, bool surfaceActive)
    {
        ArgumentNullException.ThrowIfNull(item);
        var text = item.Kind != ArtifactKind.Image;
        return
        [
            new ArtifactCommandState(ArtifactCommand.Diff, item.VersionCount > 1),
            new ArtifactCommandState(ArtifactCommand.Copy, text),
            new ArtifactCommandState(ArtifactCommand.Export, true),
            new ArtifactCommandState(ArtifactCommand.Edit, text),
            new ArtifactCommandState(ArtifactCommand.Access, item.RequestedOrigins.Count > 0),
            new ArtifactCommandState(ArtifactCommand.Stop, surfaceActive),
            new ArtifactCommandState(ArtifactCommand.Close, true),
        ];
    }
}
