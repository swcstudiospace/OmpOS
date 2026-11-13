using System.Net.Http.Headers;
using System.Security.Claims;
using Autonogrammer.AgentWorker.Configuration;
using Autonogrammer.AgentWorker.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.AgentWorker.Bridge;

/// <summary>
/// The <c>DELETE /accounts/{ownerDir}</c> endpoint the operator's erasure calls: it deletes the account directory under the workspace root.
/// The request carries a purge token: the same signature, issuer and key rules as a session token (checked by <see cref="WorkerJwtValidator"/>),
/// but with the audience <see cref="Audience"/>, <c>purpose</c> <c>purge</c>, an <c>owner_dir</c> equal to the route, and at most five minutes
/// between <c>iat</c> and <c>exp</c>. A session token never authorises a purge. Statuses: 400 malformed owner directory, 401 missing or invalid
/// token, 403 wrong purpose or owner, 404 no such directory, 409 a session of the account is attached, 204 deleted.
/// </summary>
internal static class AccountPurgeEndpoint
{
    /// <summary>Gets the route of the purge.</summary>
    public const string Route = "/accounts/{ownerDir}";

    /// <summary>Gets the audience a purge token must be issued for.</summary>
    public const string Audience = "autonogrammer-worker-purge";

    /// <summary>Gets the claim that names what a token may do.</summary>
    public const string PurposeClaimType = "purpose";

    /// <summary>Gets the purpose a purge token carries.</summary>
    public const string PurposeValue = "purge";

    private static readonly TimeSpan MaximumPurgeLifetime = TimeSpan.FromMinutes(5);
    private static readonly JsonWebTokenHandler TokenHandler = new();

    private static readonly Action<ILogger, Exception?> Purged =
        LoggerMessage.Define(LogLevel.Information, new EventId(40, "AccountPurged"), "Account directory purged.");

    private static readonly Action<ILogger, Exception?> PurgeRefused =
        LoggerMessage.Define(LogLevel.Warning, new EventId(41, "AccountPurgeRefused"), "Account purge refused: a session of the account is attached.");

    /// <summary>Maps <c>DELETE /accounts/{ownerDir}</c>. The endpoint checks its own purge token, so it opts out of the fallback policy.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The endpoint convention builder.</returns>
    public static IEndpointConventionBuilder MapAccountPurge(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapDelete(Route, HandleAsync).AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        string ownerDir,
        HttpContext context,
        WorkerJwtValidator validator,
        OmpProcessHost host,
        IOptions<WorkerOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!OwnerDirectory.IsValid(ownerDir))
        {
            return Results.StatusCode(StatusCodes.Status400BadRequest);
        }

        var principal = await AuthenticateAsync(BearerToken(context.Request), validator).ConfigureAwait(false);
        if (principal is null)
        {
            return Results.StatusCode(StatusCodes.Status401Unauthorized);
        }

        if (!IsPurgeFor(principal, ownerDir))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var workspaceRoot = options.Value.WorkspaceRoot;
        var outcome = await Task.Run(() => AccountPurge.Delete(workspaceRoot, ownerDir, host, cancellationToken), cancellationToken).ConfigureAwait(false);
        var logger = loggerFactory.CreateLogger("Autonogrammer.AgentWorker.AccountPurge");
        return outcome switch
        {
            AccountPurgeOutcome.Deleted => LogAndReturn(logger, Purged, Results.NoContent()),
            AccountPurgeOutcome.NotFound => Results.StatusCode(StatusCodes.Status404NotFound),
            _ => LogAndReturn(logger, PurgeRefused, Results.StatusCode(StatusCodes.Status409Conflict)),
        };
    }

    private static IResult LogAndReturn(ILogger logger, Action<ILogger, Exception?> message, IResult result)
    {
        message(logger, null);
        return result;
    }

    /// <summary>Validates the bearer token with the worker's own validator, with the purge audience and lifetime in place of the session ones.</summary>
    /// <param name="token">The bearer token; empty when the header is missing.</param>
    /// <param name="validator">The worker's token validator, whose keys, issuer and algorithms are reused.</param>
    /// <returns>The principal when the token is a valid purge token, otherwise <see langword="null"/>.</returns>
    private static async Task<ClaimsPrincipal?> AuthenticateAsync(string token, WorkerJwtValidator validator)
    {
        if (token.Length == 0)
        {
            return null;
        }

        var result = await TokenHandler.ValidateTokenAsync(token, PurgeParameters(validator.Parameters)).ConfigureAwait(false);
        return result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null;
    }

    private static TokenValidationParameters PurgeParameters(TokenValidationParameters worker)
    {
        var workerLifetime = worker.LifetimeValidator;
        var purge = worker.Clone();
        purge.ValidAudience = Audience;
        purge.LifetimeValidator = (notBefore, expires, token, parameters) =>
            workerLifetime is not null
            && workerLifetime(notBefore, expires, token, parameters)
            && WithinPurgeLifetime(expires, token);
        return purge;
    }

    private static bool WithinPurgeLifetime(DateTime? expires, SecurityToken token) =>
        expires is { } expiry
        && token is JsonWebToken { IssuedAt: var issuedAt } && issuedAt != DateTime.MinValue
        && expiry - issuedAt <= MaximumPurgeLifetime;

    /// <summary>Returns whether the token names the purge purpose and exactly the route's owner directory.</summary>
    private static bool IsPurgeFor(ClaimsPrincipal principal, string ownerDir)
    {
        var purposes = principal.FindAll(PurposeClaimType).ToArray();
        var owners = principal.FindAll(OwnerDirectory.ClaimType).ToArray();
        return purposes is [{ Value: PurposeValue }]
            && owners is [{ Value: var owner }]
            && string.Equals(owner, ownerDir, StringComparison.Ordinal);
    }

    private static string BearerToken(HttpRequest request) =>
        AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out var header) && header.Parameter is { } token
            ? token
            : string.Empty;
}
