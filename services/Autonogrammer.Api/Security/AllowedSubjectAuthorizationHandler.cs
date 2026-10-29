using Autonogrammer.Api.Middleware;
using Autonogrammer.Api.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Decides <see cref="AllowedSubjectRequirement"/>. The requirement is met in owner-token mode (the owner is the only identity), for
/// the Development principal, when <c>Api:Auth:AllowAnyAuthenticatedUser</c> is set, or when the caller's <c>sub</c> is in
/// <c>Api:Auth:AllowedSubjects</c> (exact, case-sensitive). An empty list admits every caller only in Development: the options validator
/// refuses an empty list elsewhere. A caller that fails the requirement is authenticated, so the answer is 403, never 404.
/// </summary>
internal sealed class AllowedSubjectAuthorizationHandler : AuthorizationHandler<AllowedSubjectRequirement>
{
    private readonly IOptions<ApiAuthOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="AllowedSubjectAuthorizationHandler"/> class.</summary>
    /// <param name="options">The validated authentication options.</param>
    public AllowedSubjectAuthorizationHandler(IOptions<ApiAuthOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowedSubjectRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = _options.Value;
        var allowed = options.Mode == ApiAuthMode.OwnerToken
            || options.AllowAnyAuthenticatedUser
            || options.AllowedSubjects.Count == 0
            || string.Equals(context.User.Identity?.AuthenticationType, AuthBoundaryMiddleware.DevelopmentAuthenticationType, StringComparison.Ordinal)
            || options.AllowedSubjects.Contains(context.User.FindFirst(JwtBearerSetup.SubjectClaimType)?.Value ?? string.Empty, StringComparer.Ordinal);
        if (allowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
