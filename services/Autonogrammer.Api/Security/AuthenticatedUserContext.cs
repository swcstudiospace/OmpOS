namespace Autonogrammer.Api.Security;

/// <summary>
/// The ambient <see cref="IUserContext"/> for hosts without the development principal. Its name is the subject claim of the bearer
/// token that JwtBearer validated for the current request, so session ownership follows the signed-in caller. With no validated
/// subject it throws rather than naming an anonymous caller.
/// </summary>
internal sealed class AuthenticatedUserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Initializes a new instance of the <see cref="AuthenticatedUserContext"/> class.</summary>
    /// <param name="httpContextAccessor">The accessor that exposes the current request.</param>
    public AuthenticatedUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public string Name =>
        _httpContextAccessor.HttpContext?.User.FindFirst(JwtBearerSetup.SubjectClaimType)?.Value
        ?? throw new InvalidOperationException("No validated bearer token subject is bound to this request.");
}
