using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>
/// What the settings and workspace endpoints may read: the workspace folder (<c>Api:Workspace:Root</c>), the user home only when
/// <c>Api:Workspace:IncludeUserScope</c> is true, and which callers may read at all (<c>Api:Workspace:Owners</c>).
/// </summary>
internal sealed class WorkspaceAccess
{
    private readonly WorkspaceLocator _locator;
    private readonly IOptions<WorkspaceSettingsOptions> _options;
    private readonly IUserHomeProvider _home;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceAccess"/> class.</summary>
    /// <param name="locator">Resolves the workspace folder.</param>
    /// <param name="options">The workspace-reading options.</param>
    /// <param name="home">Names the home directory of the API process.</param>
    public WorkspaceAccess(WorkspaceLocator locator, IOptions<WorkspaceSettingsOptions> options, IUserHomeProvider home)
    {
        _locator = locator;
        _options = options;
        _home = home;
    }

    /// <summary>Gets a value indicating whether the user directories of the process home may be read.</summary>
    public bool IncludesUserScope => _options.Value.IncludeUserScope;

    /// <summary>Gets the workspace folder when it is configured and exists, otherwise null.</summary>
    public string? ExistingRoot => _locator.Root is { } root && Directory.Exists(root) ? root : null;

    /// <summary>Gets the home directory when user scope is switched on and the directory exists, otherwise null.</summary>
    public string? UserHome
    {
        get
        {
            if (!IncludesUserScope || _home.Home is not { } home || !Path.IsPathFullyQualified(home))
            {
                return null;
            }

            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
            return Directory.Exists(full) ? full : null;
        }
    }

    /// <summary>Returns a sentence that says why the workspace cannot be read, or null when it can.</summary>
    /// <returns>The reason, or null.</returns>
    public string? UnavailableReason() =>
        _locator.Root is null
            ? "The API has no workspace folder: Api:Workspace:Root is not set to an absolute path."
            : ExistingRoot is null ? "The workspace folder of the API does not exist." : null;

    /// <summary>Returns whether a caller may read the workspace and settings endpoints.</summary>
    /// <param name="caller">The caller's subject.</param>
    /// <returns>True when no owner list is configured or the list names the caller.</returns>
    public bool Permits(string caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var owners = _options.Value.Owners;
        return owners.Count == 0 || owners.Contains(caller, StringComparer.Ordinal);
    }

    /// <summary>Builds the 404 a caller outside the owner list receives.</summary>
    /// <returns>The problem.</returns>
    public static ProblemHttpResult NotPermitted() =>
        WebProblems.Of(StatusCodes.Status404NotFound, "workspace_not_found", "Not found.", "The workspace does not exist.");
}
