using Autonogrammer.Native.Views.Conventions;
using Autonogrammer.Presentation.Commands;

namespace Autonogrammer.Native.Settings;

/// <summary>Reads the custom commands and subagents of the workspace and of the user's home for the Commands page, through the same discovery the slash palette uses. A workspace that does not exist is the page's error state.</summary>
internal sealed class ConventionsCommandsSource : ISlashDiscoverySource
{
    private readonly string _workspaceRoot;
    private readonly string? _userHome;

    /// <summary>Initializes a new instance of the <see cref="ConventionsCommandsSource"/> class.</summary>
    /// <param name="workspaceRoot">The workspace directory.</param>
    /// <param name="userHome">The user's home directory, or <see langword="null"/> to skip user commands.</param>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    public ConventionsCommandsSource(string workspaceRoot, string? userHome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = workspaceRoot;
        _userHome = userHome;
    }

    /// <inheritdoc/>
    public async Task<SlashDiscovery> LoadAsync(CancellationToken cancellationToken)
    {
        var set = await ConventionSettingsLoader.LoadAsync(_workspaceRoot, _userHome, cancellationToken).ConfigureAwait(false);
        return ConventionMapping.ToDiscovery(set);
    }
}
