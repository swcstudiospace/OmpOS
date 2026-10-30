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
public sealed record ProjectListItem(string Id, string Name, bool Archived, int MetadataEntries, int ProjectMemoryEntries);

/// <summary>Lists projects. Folders are not a member of this read.</summary>
public interface IProjectListSource
{
    /// <summary>Lists projects.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The rows.</returns>
    Task<CoworkChannelResult<IReadOnlyList<ProjectListItem>>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>The project list: loading, empty, offline, error and ready.</summary>
public sealed class ProjectListModel : ObservableModel
{
    private readonly IProjectListSource _source;
    private CoworkViewState _state = CoworkViewState.Loading;
    private string? _sentence;
    private IReadOnlyList<ProjectListItem> _rows = [];

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

    /// <summary>Loads the list.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the state is set.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Set(CoworkViewState.Loading, "Loading projects.", []);
        var result = await _source.ListAsync(cancellationToken).ConfigureAwait(false);
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

        _rows = result.Value;
        Set(result.Value.Count == 0 ? CoworkViewState.Empty : CoworkViewState.Ready, null, result.Value);
    }

    private static string? DegradedSentence(string? state) =>
        CoworkDegradedSentences.TryFor(state);

    private void Set(CoworkViewState state, string? sentence, IReadOnlyList<ProjectListItem> rows)
    {
        _state = state;
        _sentence = sentence;
        _rows = rows;
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(Sentence));
        NotifyPropertyChanged(nameof(Rows));
    }
}
