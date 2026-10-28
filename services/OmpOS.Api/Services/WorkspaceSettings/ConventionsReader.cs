using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Workspace.Conventions;

namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>
/// Reads the conventions of the server workspace with <see cref="ConventionCatalog"/> over <see cref="LocalConventionFileSource"/>, which refuses a
/// link that leaves the workspace folder. The user directories of the API process home are read only when user scope is switched on.
/// </summary>
internal sealed class ConventionsReader
{
    private readonly WorkspaceAccess _access;

    /// <summary>Initializes a new instance of the <see cref="ConventionsReader"/> class.</summary>
    /// <param name="access">What the endpoints may read.</param>
    public ConventionsReader(WorkspaceAccess access)
    {
        _access = access;
    }

    /// <summary>Discovers the conventions now. Each call reads again.</summary>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    /// <returns>What was found, or an unavailable answer that says why nothing was read.</returns>
    public async Task<ConventionsResponse> ReadAsync(CancellationToken cancellationToken)
    {
        if (_access.ExistingRoot is not { } root)
        {
            return ConventionsMapper.Unavailable(_access.UnavailableReason() ?? "The workspace folder of the API cannot be read.", _access.IncludesUserScope);
        }

        var home = _access.UserHome;
        var roots = new[] { root };
        try
        {
            var set = await ConventionCatalog.DiscoverAsync(roots, home, ConventionSources.Local(roots, home), cancellationToken: cancellationToken).ConfigureAwait(false);
            return ConventionsMapper.ToResponse(set, root, home);
        }
        catch (DirectoryNotFoundException)
        {
            return ConventionsMapper.Unavailable("The workspace folder of the API does not exist.", _access.IncludesUserScope);
        }
    }
}
