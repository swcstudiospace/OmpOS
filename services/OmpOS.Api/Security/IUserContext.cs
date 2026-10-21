namespace OmpOS.Api.Security;

/// <summary>
/// Ambient caller identity for the request. Phase 2 binds a fixed single-developer
/// principal; replace the <c>IUserContext</c> registration with a real authenticator
/// (OIDC, API keys) to enforce per-user ownership without changing endpoints.
/// </summary>
internal interface IUserContext
{
    /// <summary>Gets the caller identifier used as the default session owner.</summary>
    string Name { get; }
}
