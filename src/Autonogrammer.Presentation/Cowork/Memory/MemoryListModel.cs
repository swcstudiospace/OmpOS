#pragma warning disable MA0048 // The plan owns this file for the row, the source and the list.
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork.Memory;

/// <summary>One memory row. The source session is named on every entry.</summary>
/// <param name="Id">The entry identifier.</param>
/// <param name="Content">The entry text. It is not logged.</param>
/// <param name="SourceSession">The session that saved the entry.</param>
public sealed record MemoryListItem(string Id, string Content, string SourceSession);

/// <summary>Lists and deletes memory entries.</summary>
public interface IMemoryListSource
{
    /// <summary>Lists entries.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The rows.</returns>
    Task<CoworkChannelResult<IReadOnlyList<MemoryListItem>>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Hard-deletes one entry.</summary>
    /// <param name="id">The entry identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    Task<CoworkChannelResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken);
}

/// <summary>The memory list. Every row names its source session. Deletes are per entry.</summary>
public sealed class MemoryListModel : ObservableModel
{
    private readonly IMemoryListSource _source;
    private CoworkViewState _state = CoworkViewState.Loading;
    private string? _sentence;
    private IReadOnlyList<MemoryListItem> _rows = [];

    /// <summary>Creates the list.</summary>
    /// <param name="source">The list source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public MemoryListModel(IMemoryListSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the view state.</summary>
    public CoworkViewState State => _state;

    /// <summary>Gets the degraded sentence, or null.</summary>
    public string? Sentence => _sentence;

    /// <summary>Gets the rows.</summary>
    public IReadOnlyList<MemoryListItem> Rows => _rows;

    /// <summary>Loads the list.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the state is set.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Set(CoworkViewState.Loading, "Loading memory.", []);
        var result = await _source.ListAsync(cancellationToken).ConfigureAwait(false);
        if (result.Offline)
        {
            Set(CoworkViewState.Offline, CoworkDegradedSentences.Offline, []);
            return;
        }

        if (!result.Succeeded || result.Value is null)
        {
            Set(CoworkViewState.Error, Named(result.State) ?? "Memory could not be loaded.", []);
            return;
        }

        Set(result.Value.Count == 0 ? CoworkViewState.Empty : CoworkViewState.Ready, null, result.Value);
    }

    /// <summary>Deletes one entry.</summary>
    /// <param name="id">The entry identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the delete was accepted.</returns>
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _source.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.Offline)
        {
            Set(CoworkViewState.Offline, CoworkDegradedSentences.Offline, _rows);
            return false;
        }

        if (!result.Succeeded)
        {
            Set(CoworkViewState.Error, Named(result.State) ?? "The memory entry could not be deleted.", _rows);
            return false;
        }

        var next = _rows.Where(row => !string.Equals(row.Id, id, StringComparison.Ordinal)).ToArray();
        Set(next.Length == 0 ? CoworkViewState.Empty : CoworkViewState.Ready, null, next);
        return true;
    }

    private static string? Named(string? state) =>
        CoworkDegradedSentences.TryFor(state);

    private void Set(CoworkViewState state, string? sentence, IReadOnlyList<MemoryListItem> rows)
    {
        _state = state;
        _sentence = sentence;
        _rows = rows;
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(Sentence));
        NotifyPropertyChanged(nameof(Rows));
    }
}
