using OmpOS.Workspace;

namespace OmpOS.Native.Views.Search;

/// <summary>
/// The state of the workspace search box: the latest search's hits, its truncation flag, and its error. An invalid regular
/// expression is an error result from <see cref="WorkspaceSearch"/>, so it is shown as <see cref="Error"/> and no hits.
/// A search that a newer search overtakes never replaces the newer results.
/// </summary>
internal sealed class WorkspaceSearchModel
{
    private readonly WorkspaceSearch _search;
    private int _generation;

    /// <summary>Creates the model over a workspace search.</summary>
    /// <param name="search">The search to run.</param>
    public WorkspaceSearchModel(WorkspaceSearch search)
    {
        ArgumentNullException.ThrowIfNull(search);
        _search = search;
    }

    /// <summary>Raised when a search starts or finishes, on the thread that ran the continuation.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the hits of the latest finished search, ordered by file path, line and position.</summary>
    public IReadOnlyList<SearchHit> Hits { get; private set; } = [];

    /// <summary>Gets whether the latest finished search had more hits than it returned.</summary>
    public bool Truncated { get; private set; }

    /// <summary>Gets why the latest finished search failed, or <c>null</c> when it succeeded.</summary>
    public string? Error { get; private set; }

    /// <summary>Gets whether a search is running.</summary>
    public bool IsSearching { get; private set; }

    /// <summary>Gets whether a search has finished since the model was created.</summary>
    public bool HasSearched { get; private set; }

    /// <summary>Runs a search and publishes its result unless a newer search has started since.</summary>
    /// <param name="pattern">The text or regular expression to find.</param>
    /// <param name="isRegex">Whether the pattern is a regular expression.</param>
    /// <param name="ignoreCase">Whether matching ignores case.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>A task that completes when the search is published or cancelled.</returns>
    public async Task SearchAsync(string pattern, bool isRegex, bool ignoreCase, CancellationToken cancellationToken)
    {
        var generation = ++_generation;
        IsSearching = true;
        RaiseChanged();

        var request = new SearchRequest(pattern, isRegex ? SearchKind.Regex : SearchKind.Text, ignoreCase);
        try
        {
            var result = await _search.SearchAsync(request, cancellationToken).ConfigureAwait(true);
            Publish(generation, result.Hits, result.Truncated, result.Error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The workspace could not be read, so no hits are available.
            Publish(generation, [], truncated: false, exception.Message);
        }
        finally
        {
            if (generation == _generation)
            {
                IsSearching = false;
                RaiseChanged();
            }
        }
    }

    private void Publish(int generation, IReadOnlyList<SearchHit> hits, bool truncated, string? error)
    {
        if (generation != _generation)
        {
            return;
        }

        Hits = hits;
        Truncated = truncated;
        Error = error;
        HasSearched = true;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
