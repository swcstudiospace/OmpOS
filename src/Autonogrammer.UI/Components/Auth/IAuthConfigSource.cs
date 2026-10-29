namespace Autonogrammer.UI.Components.Auth;

/// <summary>Loads how this deployment authenticates (<c>GET /auth/config</c>). The head implements it over its API client.</summary>
public interface IAuthConfigSource
{
    /// <summary>Loads the configuration.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The configuration, or null when the API could not be reached or answered something that is not the documented shape.</returns>
    Task<AuthConfig?> LoadAsync(CancellationToken cancellationToken);
}
