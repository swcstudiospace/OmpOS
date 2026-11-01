#pragma warning disable MA0048 // The plan owns this file for the source and the editor.
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork.Projects;

/// <summary>Rename, archive and hard delete. The confirmation counts are taken before delete runs.</summary>
public interface IProjectEditorSource
{
    /// <summary>Reads the counts the hard delete will remove. This is the same read delete executes against.</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The row, including both counts.</returns>
    Task<CoworkChannelResult<ProjectListItem>> ReadAsync(string id, CancellationToken cancellationToken);

    /// <summary>Renames the project. Folders are not sent.</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="name">The new name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    Task<CoworkChannelResult<bool>> RenameAsync(string id, string name, CancellationToken cancellationToken);

    /// <summary>Archives the project.</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    Task<CoworkChannelResult<bool>> ArchiveAsync(string id, CancellationToken cancellationToken);

    /// <summary>Hard-deletes the project. Called only after the confirmation is accepted.</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    Task<CoworkChannelResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken);
}

/// <summary>The project editor. Delete shows both counts before the hard delete runs.</summary>
public sealed class ProjectEditorModel : ObservableModel
{
    private readonly IProjectEditorSource _source;
    private string _name = string.Empty;
    private int _metadataEntries;
    private int _projectMemoryEntries;
    private bool _confirmationReady;
    private bool _deleteRan;
    private CoworkViewState _state = CoworkViewState.Loading;
    private string? _sentence;

    /// <summary>Creates the editor.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public ProjectEditorModel(IProjectEditorSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the project identifier, or null before open.</summary>
    public string? Id { get; private set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name
    {
        get => _name;
        set
        {
            _name = value ?? string.Empty;
            NotifyPropertyChanged(nameof(Name));
        }
    }

    /// <summary>Gets the view state.</summary>
    public CoworkViewState State => _state;

    /// <summary>Gets the degraded sentence, or null.</summary>
    public string? Sentence => _sentence;

    /// <summary>Gets the metadata entry count carried by the confirmation.</summary>
    public int MetadataEntries => _metadataEntries;

    /// <summary>Gets the project memory entry count carried by the confirmation.</summary>
    public int ProjectMemoryEntries => _projectMemoryEntries;

    /// <summary>Gets whether the confirmation has been prepared and delete has not run.</summary>
    public bool ConfirmationReady => _confirmationReady;

    /// <summary>Gets whether the hard delete has run.</summary>
    public bool DeleteRan => _deleteRan;

    /// <summary>Gets the confirmation sentence. Both counts are in the sentence before delete runs.</summary>
    public string DeleteConfirmation =>
        $"Delete this project? This removes {_metadataEntries} metadata entries and {_projectMemoryEntries} project memory entries.";

    /// <summary>Opens the editor from the same read delete will use.</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the editor is ready or degraded.</returns>
    public async Task OpenAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        _confirmationReady = false;
        _deleteRan = false;
        Set(CoworkViewState.Loading, "Loading project.");
        var read = await _source.ReadAsync(id, cancellationToken).ConfigureAwait(false);
        ApplyRead(read);
    }

    /// <summary>Renames the open project.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the rename was accepted.</returns>
    public async Task<bool> RenameAsync(CancellationToken cancellationToken)
    {
        if (Id is null || State is CoworkViewState.Offline or CoworkViewState.Error)
        {
            return false;
        }

        var result = await _source.RenameAsync(Id, Name, cancellationToken).ConfigureAwait(false);
        return ApplyWrite(result, "The project could not be renamed.");
    }

    /// <summary>Archives the open project.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the archive was accepted.</returns>
    public async Task<bool> ArchiveAsync(CancellationToken cancellationToken)
    {
        if (Id is null)
        {
            return false;
        }

        var result = await _source.ArchiveAsync(Id, cancellationToken).ConfigureAwait(false);
        if (ApplyWrite(result, "The project could not be archived."))
        {
            Set(CoworkViewState.Ready, CoworkDegradedSentences.ProjectArchived);
            return true;
        }

        return false;
    }

    /// <summary>Prepares the confirmation from the current counts. Delete has not run.</summary>
    public void PrepareDelete()
    {
        _deleteRan = false;
        _confirmationReady = true;
        NotifyPropertyChanged(nameof(ConfirmationReady));
        NotifyPropertyChanged(nameof(DeleteConfirmation));
        NotifyPropertyChanged(nameof(DeleteRan));
    }

    /// <summary>Runs the hard delete. Refused until <see cref="PrepareDelete"/> has run.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the delete was accepted.</returns>
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken)
    {
        if (Id is null || !_confirmationReady || _deleteRan)
        {
            return false;
        }

        var result = await _source.DeleteAsync(Id, cancellationToken).ConfigureAwait(false);
        if (!ApplyWrite(result, "The project could not be deleted."))
        {
            return false;
        }

        _deleteRan = true;
        _confirmationReady = false;
        Set(CoworkViewState.Empty, null);
        NotifyPropertyChanged(nameof(DeleteRan));
        NotifyPropertyChanged(nameof(ConfirmationReady));
        return true;
    }

    private void ApplyRead(CoworkChannelResult<ProjectListItem> read)
    {
        if (read.Offline)
        {
            Set(CoworkViewState.Offline, CoworkDegradedSentences.Offline);
            return;
        }

        if (!read.Succeeded || read.Value is null)
        {
            Set(CoworkViewState.Error, Named(read.State) ?? "The project could not be opened.");
            return;
        }

        _metadataEntries = read.Value.MetadataEntries;
        _projectMemoryEntries = read.Value.ProjectMemoryEntries;
        Name = read.Value.Name;
        Set(CoworkViewState.Ready, read.Value.Archived ? CoworkDegradedSentences.ProjectArchived : null);
        NotifyPropertyChanged(nameof(MetadataEntries));
        NotifyPropertyChanged(nameof(ProjectMemoryEntries));
        NotifyPropertyChanged(nameof(DeleteConfirmation));
    }

    private bool ApplyWrite(CoworkChannelResult<bool> result, string fallback)
    {
        if (result.Offline)
        {
            Set(CoworkViewState.Offline, CoworkDegradedSentences.Offline);
            return false;
        }

        if (!result.Succeeded)
        {
            Set(CoworkViewState.Error, Named(result.State) ?? fallback);
            return false;
        }

        return true;
    }

    private static string? Named(string? state) =>
        CoworkDegradedSentences.TryFor(state);

    private void Set(CoworkViewState state, string? sentence)
    {
        _state = state;
        _sentence = sentence;
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(Sentence));
    }
}
