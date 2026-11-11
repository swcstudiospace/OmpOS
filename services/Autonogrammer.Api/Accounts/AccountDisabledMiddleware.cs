using Autonogrammer.Api.Services;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Accounts;

/// <summary>Returns 404 for account routes when disabled. Runs after authorization so anonymous requests remain unauthorized.</summary>
internal sealed class AccountDisabledMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="AccountDisabledMiddleware"/> class.</summary>
    /// <param name="next">The next middleware.</param>
    public AccountDisabledMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Runs the middleware.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="options">The account options.</param>
    /// <returns>A task that completes when the response is written or the next middleware returns.</returns>
    public Task InvokeAsync(HttpContext context, IOptions<AccountsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Value.Enabled && context.GetEndpoint()?.Metadata.GetMetadata<AccountRouteMarker>() is not null)
        {
            return WebProblems.Of(StatusCodes.Status404NotFound, "account_disabled", "Accounts are disabled.", "Accounts are not enabled on this server.")
                .ExecuteAsync(context);
        }

        return _next(context);
    }
}
