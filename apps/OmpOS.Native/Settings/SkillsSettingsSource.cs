using OmpOS.Presentation.Settings.Customize;
using OmpOS.Workspace;

namespace OmpOS.Native.Settings;

/// <summary>Lists the skill files of the workspace (<c>.ompos/skills</c>) for the Skills page. Files that cannot be read are reported as problems.</summary>
internal sealed class SkillsSettingsSource : ISkillsSettingsSource
{
    private readonly string _workspaceRoot;

    /// <summary>Initializes a new instance of the <see cref="SkillsSettingsSource"/> class.</summary>
    /// <param name="workspaceRoot">The workspace directory.</param>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    public SkillsSettingsSource(string workspaceRoot)
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
                return WorkspaceSnapshotReader.ToCatalog(snapshot.Skills, snapshot, WorkspaceConventions.SkillsFolder);
            },
            cancellationToken);
}
