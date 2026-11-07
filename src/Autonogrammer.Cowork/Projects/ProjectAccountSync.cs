using Autonogrammer.Contracts.Cowork.Projects;

namespace Autonogrammer.Cowork.Projects;

/// <summary>
/// Syncs project metadata per account. Folders stay in the local store and are never placed in a request body.
/// Offline, 401 and 429 serve the last copy read-only. A write-back needs a fresh 200 sync.
/// </summary>
public sealed class ProjectAccountSync
{
    private readonly IProjectApi _api;
    private readonly CoworkProjectStore _store;
    private readonly List<ProjectSyncRecord> _lastRemote = [];
    private string? _lastGlobalInstructions;

    /// <summary>Initializes a new instance of the <see cref="ProjectAccountSync"/> class.</summary>
    /// <param name="api">The account API.</param>
    /// <param name="store">The local store. Folders live here only.</param>
    public ProjectAccountSync(IProjectApi api, CoworkProjectStore store)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(store);
        _api = api;
        _store = store;
    }

    /// <summary>Gets whether the last copy is read-only. True until a fresh 200 sync.</summary>
    public bool IsReadOnly { get; private set; } = true;

    /// <summary>Gets the last <see cref="ProjectSyncState"/>.</summary>
    public string State { get; private set; } = ProjectSyncState.Offline;

    /// <summary>Gets the last fetched global instructions, including the offline cache.</summary>
    public string? GlobalInstructions => _lastGlobalInstructions;

    /// <summary>Pulls metadata. On failure the last copy stays and writes are refused.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The state after the pull.</returns>
    public async Task<string> SyncAsync(CancellationToken cancellationToken)
    {
        var listed = await _api.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!listed.Succeeded || listed.Value is null)
        {
            IsReadOnly = true;
            State = listed.State;
            return State;
        }

        _lastRemote.Clear();
        _lastRemote.AddRange(listed.Value);
        ApplyRemote(listed.Value);
        var global = await _api.GetGlobalInstructionsAsync(cancellationToken).ConfigureAwait(false);
        if (global.Succeeded)
        {
            _lastGlobalInstructions = global.Value;
        }

        IsReadOnly = false;
        State = ProjectSyncState.Synced;
        return State;
    }

    /// <summary>Pushes one project's metadata. Refused while read-only. The body is built from <see cref="ProjectSyncRecord"/>, which has no folders.</summary>
    /// <param name="project">The local project. Folders are not copied onto the record.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    public async Task<ProjectApiResult<ProjectSyncRecord>> PushAsync(CoworkProject project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.ArchivedAt is not null)
        {
            IsReadOnly = true;
            State = ProjectSyncState.ProjectArchived;
            return new ProjectApiResult<ProjectSyncRecord>(false, null, false, null, ProjectSyncState.ProjectArchived, ProjectSyncState.ProjectArchived);
        }

        if (IsReadOnly)
        {
            return new ProjectApiResult<ProjectSyncRecord>(false, null, string.Equals(State, ProjectSyncState.Offline, StringComparison.Ordinal), null, State, State);
        }

        var record = ToRecord(project);
        var result = await _api.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            IsReadOnly = true;
            State = result.State;
            return result;
        }

        State = ProjectSyncState.Synced;
        return result;
    }

    /// <summary>Returns the local projects, including folders. This is the last copy when offline.</summary>
    /// <returns>The local projects.</returns>
    public IReadOnlyList<CoworkProject> ListLocal() => _store.List(includeArchived: true);

    /// <summary>Maps metadata only. Folders are omitted on purpose.</summary>
    /// <param name="project">The local project.</param>
    /// <returns>The sync record.</returns>
    public static ProjectSyncRecord ToRecord(CoworkProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new ProjectSyncRecord
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Instructions = project.Instructions,
            ContextUrls = project.ContextUrls,
            UseAccountMemory = project.UseAccountMemory,
            ArchivedAt = project.ArchivedAt,
            UpdatedAt = project.UpdatedAt,
        };
    }

    private void ApplyRemote(IReadOnlyList<ProjectSyncRecord> remote)
    {
        var local = _store.List(includeArchived: true).ToDictionary(static project => project.Id, StringComparer.Ordinal);
        foreach (var record in remote)
        {
            local.TryGetValue(record.Id, out var existing);
            _store.Upsert(new CoworkProject
            {
                Id = record.Id,
                Name = record.Name,
                Description = record.Description,
                Instructions = record.Instructions,
                ContextUrls = record.ContextUrls ?? [],
                Folders = existing?.Folders ?? [],
                UseAccountMemory = record.UseAccountMemory,
                ArchivedAt = record.ArchivedAt,
                UpdatedAt = record.UpdatedAt,
            });
        }
    }
}
