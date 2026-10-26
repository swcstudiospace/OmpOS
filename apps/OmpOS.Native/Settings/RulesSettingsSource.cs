using OmpOS.Presentation.Settings.Customize;
using OmpOS.Workspace;

namespace OmpOS.Native.Settings;

/// <summary>Lists the rule files of the workspace (<c>.ompos/rules</c>) for the Rules page. Files that cannot be read are reported as problems.</summary>
internal sealed class RulesSettingsSource : IRulesSettingsSource
{
    private readonly string _workspaceRoot;

    /// <summary>Initializes a new instance of the <see cref="RulesSettingsSource"/> class.</summary>
    /// <param name="workspaceRoot">The workspace directory.</param>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    public RulesSettingsSource(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = workspaceRoot;
    }

    /// <inheritdoc/>
    public Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken) =>
        Task.Run(
            () =>
            {
                var snapshot = WorkspaceSnapshotReader.Read(_workspaceRoot);
                return WorkspaceSnapshotReader.ToCatalog(snapshot.Rules, snapshot, WorkspaceConventions.RulesFolder);
            },
            cancellationToken);
}
