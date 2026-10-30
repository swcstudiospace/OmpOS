using Autonogrammer.Native.Views.Conventions;
using Autonogrammer.Presentation.Settings.Customize;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// Lists the rules and instruction files of the workspace and of the user's home for the Rules page: <c>rules/*.mdc</c> and <c>rules/*.md</c> in <c>.autonogrammer</c>, <c>.omp</c>, <c>.cursor</c>, <c>.claude</c>, <c>.codex</c> and <c>.agents</c>, nested rule
/// scopes, and <c>AGENTS.md</c>, <c>CLAUDE.md</c> and <c>.cursorrules</c>. Files that cannot be used are reported as problems.
/// </summary>
internal sealed class RulesSettingsSource : IRulesSettingsSource
{
    private readonly string _workspaceRoot;
    private readonly string? _userHome;

    /// <summary>Initializes a new instance of the <see cref="RulesSettingsSource"/> class.</summary>
    /// <param name="workspaceRoot">The workspace directory.</param>
    /// <param name="userHome">The user's home directory, or <see langword="null"/> to skip user rules.</param>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    public RulesSettingsSource(string workspaceRoot, string? userHome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = workspaceRoot;
        _userHome = userHome;
    }

    /// <inheritdoc/>
    public async Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken)
    {
        var set = await ConventionSettingsLoader.LoadAsync(_workspaceRoot, _userHome, cancellationToken).ConfigureAwait(false);
        return ConventionMapping.ToRulesCatalog(set);
    }
}
