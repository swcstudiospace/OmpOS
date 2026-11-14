#pragma warning disable MA0048 // The plan owns this file for the row, the source and the list.
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork.Projects;

/// <summary>One project row. Counts come from the same read the hard delete will use.</summary>
/// <param name="Id">The project identifier.</param>
/// <param name="Name">The project name.</param>
/// <param name="Archived">True when the project is archived.</param>
/// <param name="MetadataEntries">Metadata entries the delete removes.</param>
/// <param name="ProjectMemoryEntries">Project memory entries the delete removes.</param>
public sealed record ProjectListItem(string Id, string Name, bool Archived, int MetadataEntries, int ProjectMemoryEntries)
{
    /// <summary>Returns the project name, which is what a picker shows.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => Name;
}

/// <summary>Lists projects. Folders are not a member of this read.</summary>
public interface IProjectListSource
{
    /// <summary>Lists projects.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The rows.</returns>
    Task<CoworkChannelResult<IReadOnlyList<ProjectListItem>>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The project list: loading, empty, offline, error and ready, and the one project selected for the next task.
/// A selection is only ever an active row of the list. <see cref="Clear"/> drops the rows and the selection when the account changes or signs out,
/// and a load that started before the clear never applies its rows.
/// </summary>
public sealed class ProjectListModel : ObservableModel
{
    private readonly IProjectListSource _source;
    private CoworkViewState _state = CoworkViewState.Loading;
    private string? _sentence;
    private IReadOnlyList<ProjectListItem> _rows = [];
    private string? _selectedId;
    private int _generation;

    /// <summary>Creates the list.</summary>
    /// <param name="source">The list source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public ProjectListModel(IProjectListSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the view state.</summary>
    public CoworkViewState State => _state;

    /// <summary>Gets the degraded sentence, or null.</summary>
    public string? Sentence => _sentence;

    /// <summary>Gets the rows.</summary>
    public IReadOnlyList<ProjectListItem> Rows => _rows;

    /// <summary>Gets the identifier of the selected project, or null when no project is selected (the next task is unlinked).</summary>
    public string? SelectedId => _selectedId;

    /// <summary>Gets the selected row, or null.</summary>
    public ProjectListItem? Selected => _selectedId is null ? null : _rows.FirstOrDefault(row => string.Equals(row.Id, _selectedId, StringComparison.Ordinal));

    /// <summary>Loads the list. A load that finishes after <see cref="Clear"/> is dropped.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the state is set.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var generation = _generation;
        Set(CoworkViewState.Loading, "Loading projects.", []);
        var result = await _source.ListAsync(cancellationToken).ConfigureAwait(false);
        if (generation != _generation)
        {
            return;
        }

        if (result.Offline)
        {
            Set(CoworkViewState.Offline, CoworkDegradedSentences.Offline, []);
            return;
        }

        if (!result.Succeeded || result.Value is null)
        {
            Set(CoworkViewState.Error, DegradedSentence(result.State) ?? "Projects could not be loaded.", []);
            return;
        }

        Set(result.Value.Count == 0 ? CoworkViewState.Empty : CoworkViewState.Ready, DegradedSentence(result.State), result.Value);
    }

    /// <summary>
    /// Selects the project the next task is linked to. Null clears the selection. An identifier that is not an active row of the list is refused:
    /// an archived project, a project of another account, or one that is not on this device.
    /// </summary>
    /// <param name="id">The project identifier, or null.</param>
    /// <returns>True when the selection now matches <paramref name="id"/>; false when the project was refused.</returns>
    public bool Select(string? id)
    {
        if (id is null)
        {
            SetSelection(null);
            return true;
        }

        var row = _rows.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (row is null || row.Archived)
        {
            return false;
        }

        SetSelection(id);
        return true;
    }

    /// <summary>
    /// Drops the rows and the selection, and refuses any load still in flight. The head calls this on sign-out and on an account switch,
    /// so no project of the previous account stays selected.
    /// </summary>
    /// <param name="sentence">The sentence to show, or null.</param>
    public void Clear(string? sentence)
    {
        _generation++;
        _selectedId = null;
        Set(CoworkViewState.Empty, sentence, []);
    }

    private static string? DegradedSentence(string? state) =>
        CoworkDegradedSentences.TryFor(state);

    private void SetSelection(string? id)
    {
        if (string.Equals(_selectedId, id, StringComparison.Ordinal))
        {
            return;
        }

        _selectedId = id;
        NotifyPropertyChanged(nameof(SelectedId));
        NotifyPropertyChanged(nameof(Selected));
    }

    private void Set(CoworkViewState state, string? sentence, IReadOnlyList<ProjectListItem> rows)
    {
        _state = state;
        _sentence = sentence;
        _rows = rows;
        if (state != CoworkViewState.Loading && _selectedId is not null && !rows.Any(row => string.Equals(row.Id, _selectedId, StringComparison.Ordinal) && !row.Archived))
        {
            _selectedId = null;
            NotifyPropertyChanged(nameof(SelectedId));
            NotifyPropertyChanged(nameof(Selected));
        }

        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(Sentence));
        NotifyPropertyChanged(nameof(Rows));
    }
}
