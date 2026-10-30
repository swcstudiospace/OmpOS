using Autonogrammer.Presentation.Settings;
using Autonogrammer.Workspace.Conventions;

namespace Autonogrammer.Native.Settings;

/// <summary>Discovers the conventions of a workspace for the Rules, Skills and Commands pages, with the skills that ship with the application. A workspace that does not exist is the page's error state.</summary>
internal static class ConventionSettingsLoader
{
    /// <summary>Discovers the conventions of the workspace root and the user's home.</summary>
    /// <param name="workspaceRoot">The workspace directory.</param>
    /// <param name="userHome">The user's home directory, or <see langword="null"/> to skip user conventions.</param>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    /// <returns>What was found, and every problem.</returns>
    /// <exception cref="SettingsSourceException">The workspace directory does not exist.</exception>
    public static async Task<ConventionSet> LoadAsync(string workspaceRoot, string? userHome, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (!Directory.Exists(workspaceRoot))
        {
            throw new SettingsSourceException(HeadSettingsCopy.WorkspaceUnreadable);
        }

        var roots = new[] { Path.GetFullPath(workspaceRoot) };
        return await ConventionCatalog.DiscoverAsync(roots, userHome, ConventionSources.Local(roots, userHome).WithBuiltin(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
