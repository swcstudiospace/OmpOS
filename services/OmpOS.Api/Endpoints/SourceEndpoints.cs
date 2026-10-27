using OmpOS.Api.Services;
using OmpOS.Contracts.Catalog;

namespace OmpOS.Api.Endpoints;

/// <summary>
/// <c>GET /source</c>: the AGPL-3.0 section 13 source offer. It is anonymous, like <c>/health/live</c>, because the offer must reach every user
/// who talks to the service, signed in or not (ADR-0018). It answers only constants and the build's revision; it reads no session.
/// </summary>
internal static class SourceEndpoints
{
    /// <summary>Maps the source-offer endpoint.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapSourceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/source", static (SourceInfoProvider provider) => TypedResults.Ok(provider.Info))
            .AllowAnonymous()
            .WithTags("Source")
            .WithSummary("Names the licence and the source repository of this running service (AGPL-3.0-or-later section 13).")
            .Produces<SourceInfo>(StatusCodes.Status200OK);
        return endpoints;
    }
}
