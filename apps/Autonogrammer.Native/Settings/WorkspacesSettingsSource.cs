using System.Globalization;
using Autonogrammer.Presentation.Editor;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Workspaces;
using Autonogrammer.Workspace.Sets;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// Feeds the Workspaces page from the user's workspace set store, the same file the <c>/save-workspace</c> and <c>/load-workspace</c> commands keep. A set is stored
/// under an identifier derived from its name (<see cref="SetIdFor"/>), so saving a name again replaces the set of that name. The folder editor of the page
/// checks and exchanges <c>.code-workspace</c> files through <see cref="WorkspaceSetPickerBackend"/>.
/// </summary>
internal sealed class WorkspacesSettingsSource : IWorkspacesSettingsSource
{
    private const int MaxNameChars = 80;

    private static readonly char[] NameSeparators = [' ', '-', '_'];

    private readonly WorkspaceSetStore _store;

    /// <summary>Initializes a new instance of the <see cref="WorkspacesSettingsSource"/> class over the local file system.</summary>
    /// <param name="store">The store of the recent sets. The caller owns it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public WorkspacesSettingsSource(WorkspaceSetStore store)
        : this(store, new WorkspaceSetPickerBackend())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WorkspacesSettingsSource"/> class with the given checks.</summary>
    /// <param name="store">The store of the recent sets. The caller owns it.</param>
    /// <param name="pickerBackend">The checks and file access of the folder editor.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal WorkspacesSettingsSource(WorkspaceSetStore store, IWorkspaceSetPickerBackend pickerBackend)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(pickerBackend);
        _store = store;
        PickerBackend = pickerBackend;
    }

    /// <inheritdoc/>
    public IWorkspaceSetPickerBackend PickerBackend { get; }

    /// <summary>
    /// Gets the identifier a set has in the store. A name of ASCII letters, digits, spaces, hyphens and underscores gets the library's identifier form
    /// (lower case, runs of other characters become one hyphen), so <c>Team A</c> and <c>team-a</c> are one set, as a set made from paths with that name is.
    /// Any other name is its trimmed lower-case text, so names that only differ in script or punctuation stay different sets.
    /// </summary>
    /// <param name="name">The name of the set.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or white space.</exception>
    internal static string SetIdFor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var lower = CultureInfo.InvariantCulture.TextInfo.ToLower(name.Trim());
        if (!lower.All(static character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or ' ' or '-' or '_'))
        {
            return lower;
        }

        var words = lower.Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0 ? lower : string.Join('-', words);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkspaceRecentRow>> ListRecentsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<RecentWorkspaceSet> recents;
        try
        {
            recents = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SettingsSourceException(WorkspacesSourceCopy.ReadFailed(exception.Message), exception);
        }

        return [.. recents.Select(ToRow)];
    }

    /// <inheritdoc/>
    public async Task SaveAsync(string name, IReadOnlyList<WorkspacePickerRoot> roots, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(roots);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SettingsSourceException(WorkspacesSourceCopy.NameBlank);
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameChars || trimmed.Any(char.IsControl))
        {
            throw new SettingsSourceException(WorkspacesSourceCopy.NameUnusable);
        }

        if (roots.Count == 0)
        {
            throw new SettingsSourceException(WorkspacesSourceCopy.NothingToSave);
        }

        var set = new WorkspaceSet(SetIdFor(trimmed), trimmed, WorkspaceSetPickerBackend.ToSetRoots(roots));
        try
        {
            await _store.RecordAsync(set, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SettingsSourceException(WorkspacesSourceCopy.SaveFailed(exception.Message), exception);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ForgetAsync(string setId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setId);
        try
        {
            return await _store.ForgetAsync(setId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SettingsSourceException(WorkspacesSourceCopy.ForgetFailed(exception.Message), exception);
        }
    }

    private static WorkspaceRecentRow ToRow(RecentWorkspaceSet recent) => new(
        recent.Set.Id,
        recent.Set.Name,
        recent.Set.Roots.Count,
        recent.Set.PrimaryPath,
        recent.LastUsedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
        WorkspaceSetPickerBackend.ToPickerRoots(recent.Set));
}
