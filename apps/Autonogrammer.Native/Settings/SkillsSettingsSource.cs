using Autonogrammer.Native.Views.Conventions;
using Autonogrammer.Presentation.Settings.Customize;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// Lists the skills of the workspace and of the user's home for the Skills page: <c>skills/&lt;name&gt;/SKILL.md</c> in <c>.autonogrammer</c>, <c>.omp</c>, <c>.cursor</c>, <c>.claude</c>, <c>.codex</c> and <c>.agents</c>, and nested skill scopes. Files that cannot be used are
/// reported as problems.
/// </summary>
internal sealed class SkillsSettingsSource : ISkillsSettingsSource
{
    private readonly string _workspaceRoot;
    private readonly string? _userHome;

    /// <summary>Initializes a new instance of the <see cref="SkillsSettingsSource"/> class.</summary>
    /// <param name="workspaceRoot">The workspace directory.</param>
    /// <param name="userHome">The user's home directory, or <see langword="null"/> to skip user skills.</param>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    public SkillsSettingsSource(string workspaceRoot, string? userHome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = workspaceRoot;
        _userHome = userHome;
    }

    /// <inheritdoc/>
    public async Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken)
    {
        var set = await ConventionSettingsLoader.LoadAsync(_workspaceRoot, _userHome, cancellationToken).ConfigureAwait(false);
        return ConventionMapping.ToSkillsCatalog(set);
    }
}
