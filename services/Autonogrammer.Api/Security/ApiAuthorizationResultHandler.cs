using Autonogrammer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Answers an authenticated caller that fails <see cref="AllowedSubjectRequirement"/> with a 403 ProblemDetails body. Every other
/// outcome goes to the framework's default handler, so 401 challenges and other forbids keep their behaviour.
/// </summary>
internal sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    /// <inheritdoc />
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);
        if (authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure is { } failure
            && failure.FailedRequirements.OfType<AllowedSubjectRequirement>().Any())
        {
            var problem = WebProblems.Of(
                StatusCodes.Status403Forbidden,
                "subject_not_allowed",
                "Forbidden.",
                "This account is not permitted to use this deployment.");
            await problem.ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult).ConfigureAwait(false);
    }
}
