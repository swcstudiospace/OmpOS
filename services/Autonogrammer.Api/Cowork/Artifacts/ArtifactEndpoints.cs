using System.Buffers;
using System.Text;
using Autonogrammer.Api.Hosting;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts;
using Autonogrammer.Cowork.Artifacts.Rendering;
using Autonogrammer.Cowork.Artifacts.Stores;
using Autonogrammer.Cowork.Artifacts.Tools;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Owner-scoped artifact routes and the anonymous ticketed render route.</summary>
internal static class ArtifactEndpoints
{
    /// <summary>Image PUT body ceiling. Hosting's global Kestrel cap is not raised.</summary>
    public const int ImageBodyCeiling = 6 * 1024 * 1024;

    /// <summary>Non-image PUT body ceiling.</summary>
    public const int TextBodyCeiling = 1024 * 1024;

    /// <summary>Maps the artifact routes.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapCoworkArtifactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        Map(endpoints, "/cowork/artifacts", ListAsync, "ListCoworkArtifacts", "Lists the caller's artifacts.");
        Map(endpoints, "/cowork/artifacts/{id}", GetAsync, "GetCoworkArtifact", "Reads one artifact header the caller owns.");
        Map(endpoints, "/cowork/artifacts/{id}/versions/{n:int}", GetVersionAsync, "GetCoworkArtifactVersion", "Reads one owned version header.");
        endpoints.MapGet("/cowork/artifacts/{id}/versions/{n:int}/content", ContentAsync)
            .WithName("GetCoworkArtifactContent")
            .WithTags("Cowork artifacts")
            .WithSummary("Downloads owned version bytes as an attachment. The API origin never renders them.")
            .WithMetadata(ArtifactRouteMarker.Instance)
            .RequireRateLimiting(ArtifactOwnerRateLimiterPolicy.PolicyName)
            .Produces(StatusCodes.Status200OK, contentType: ArtifactWireContract.MediaType)
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPut("/cowork/artifacts/{id}/versions/{n:int}", PutAsync)
            .WithName("PutCoworkArtifactVersion")
            .WithTags("Cowork artifacts")
            .WithSummary("Stores a version idempotently by hash. Same bytes answer 200; different bytes answer 409.")
            .WithMetadata(ArtifactRouteMarker.Instance)
            .RequireRateLimiting(ArtifactOwnerRateLimiterPolicy.PolicyName)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413RequestEntityTooLarge);
        MapMutations(endpoints);
        MapRender(endpoints);
        return endpoints;
    }

    private static void MapMutations(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/cowork/artifacts/{id}", DeleteAsync)
            .WithName("DeleteCoworkArtifact")
            .WithTags("Cowork artifacts")
            .WithSummary("Deletes an artifact the caller owns. Another owner's id is the same 404 as an absent id.")
            .WithMetadata(ArtifactRouteMarker.Instance)
            .RequireRateLimiting(ArtifactOwnerRateLimiterPolicy.PolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPost("/cowork/artifacts/{id}/versions/{n:int}/render-tickets", IssueTicketAsync)
            .WithName("CreateCoworkArtifactRenderTicket")
            .WithTags("Cowork artifacts")
            .WithSummary("Issues a 60-second HMAC render ticket for an owned version.")
            .WithMetadata(ArtifactRouteMarker.Instance)
            .RequireRateLimiting(ArtifactOwnerRateLimiterPolicy.PolicyName)
            .Produces<RenderTicketDocument>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapGet("/cowork/artifacts/{id}/versions/{n:int}/grants", GetGrantsAsync)
            .WithName("GetCoworkArtifactGrants")
            .WithTags("Cowork artifacts")
            .WithSummary("Lists grants bound to an owned version.")
            .WithMetadata(ArtifactRouteMarker.Instance)
            .RequireRateLimiting(ArtifactOwnerRateLimiterPolicy.PolicyName)
            .Produces<List<ArtifactGrantDocument>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPut("/cowork/artifacts/{id}/versions/{n:int}/grants", PutGrantAsync)
            .WithName("PutCoworkArtifactGrant")
            .WithTags("Cowork artifacts")
            .WithSummary("Binds or revokes a grant for an owned version hash.")
            .WithMetadata(ArtifactRouteMarker.Instance)
            .RequireRateLimiting(ArtifactOwnerRateLimiterPolicy.PolicyName)
            .Produces<ArtifactGrantDocument>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapRender(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/cowork/artifact-render/{ticket}", RenderAsync)
            .WithName("GetCoworkArtifactRender")
            .WithTags("Cowork artifacts")
            .WithSummary("Renders a version from a 60-second ticket. Anonymous. Limiter partition artifact-render.")
            .WithMetadata(ArtifactRouteMarker.Instance)
            .WithMetadata(OwnResponseSecurityHeadersMetadata.Instance)
            .AllowAnonymous()
            .RequireRateLimiting(ArtifactRenderRateLimiterPolicy.PolicyName)
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void Map(IEndpointRouteBuilder endpoints, string pattern, Delegate handler, string name, string summary)
    {
        endpoints.MapGet(pattern, handler)
            .WithName(name)
            .WithTags("Cowork artifacts")
            .WithSummary(summary)
            .WithMetadata(ArtifactRouteMarker.Instance)
            .RequireRateLimiting(ArtifactOwnerRateLimiterPolicy.PolicyName)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(IUserContext user, ArtifactAccountDirectory directory, CancellationToken cancellationToken)
    {
        var rows = await directory.ListAsync(user.Name, cancellationToken).ConfigureAwait(false);
        return Results.Json(new ArtifactListDocument(rows), ArtifactsJsonContext.Default.ArtifactListDocument);
    }

    private static async Task<IResult> GetAsync(string id, IUserContext user, ArtifactAccountDirectory directory, CancellationToken cancellationToken)
    {
        var row = await directory.FindAsync(user.Name, id, cancellationToken).ConfigureAwait(false);
        return row is null ? Missing(id) : Results.Json(row, ArtifactsJsonContext.Default.ArtifactSummaryDocument);
    }

    private static async Task<IResult> GetVersionAsync(string id, int n, IUserContext user, ArtifactAccountDirectory directory, CancellationToken cancellationToken)
    {
        var version = await directory.VersionAsync(user.Name, id, n, cancellationToken).ConfigureAwait(false);
        return version is null ? Missing(id) : Results.Json(version, ArtifactsJsonContext.Default.ArtifactSummaryDocument);
    }

    private static async Task<IResult> ContentAsync(string id, int n, IUserContext user, ArtifactAccountDirectory directory, CancellationToken cancellationToken)
    {
        var bytes = await directory.ContentAsync(user.Name, id, n, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return Missing(id);
        }

        return Results.File(bytes, ArtifactWireContract.MediaType, "artifact.bin", enableRangeProcessing: false);
    }

    private static async Task<IResult> PutAsync(
        string id,
        int n,
        HttpContext context,
        IUserContext user,
        ArtifactAccountDirectory directory,
        ArtifactMetrics metrics,
        ArtifactAudit audit,
        IOptions<ArtifactOptions> limits,
        CancellationToken cancellationToken)
    {
        var kindText = context.Request.Headers[ArtifactWireContract.KindHeader].ToString();
        var image = string.Equals(kindText, nameof(ArtifactKind.Image), StringComparison.Ordinal);
        var ceiling = image ? ImageBodyCeiling : TextBodyCeiling;
        var read = await ReadVersionBodyAsync(context, ceiling, kindText, metrics, audit, user.Name, id, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }



        var metadata = context.Request.ContentType is not null && context.Request.ContentType.StartsWith(ArtifactWireContract.MetadataMediaType, StringComparison.OrdinalIgnoreCase);
        if (metadata)
        {
            context.Response.Headers[ArtifactWireContract.SameHashHeader] = "true";
            return Results.StatusCode(StatusCodes.Status200OK);
        }

        var declared = context.Request.Headers[ArtifactWireContract.ContentSha256Header].ToString();
        var hash = ArtifactWireContract.Hash(read.Bytes);
        if (!metadata && declared.Length > 0 && !string.Equals(declared, hash, StringComparison.OrdinalIgnoreCase))
        {
            metrics.Add(kindText, "rejected");
            return WebProblems.Of(StatusCodes.Status400BadRequest, "hash_mismatch", "Hash mismatch.", "The declared hash does not match the body.");
        }

        if (!Enum.TryParse<ArtifactKind>(kindText, out var kind))
        {
            kind = ArtifactKind.Markdown;
            kindText = nameof(ArtifactKind.Markdown);
        }

        ArtifactPutOutcome outcome;
        try
        {
            outcome = await directory.PutAsync(user.Name, id, n, kind, metadata ? [] : read.Bytes, hash, limits.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (ArtifactQuotaException)
        {
            metrics.Add(kindText, "rejected");
            return QuotaExceeded();
        }

        return await FinishPutAsync(context, user, metrics, audit, id, kindText, hash, outcome, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> FinishPutAsync(HttpContext context, IUserContext user, ArtifactMetrics metrics, ArtifactAudit audit, string id, string kindText, string hash, ArtifactPutOutcome outcome, CancellationToken cancellationToken)
    {
        if (outcome == ArtifactPutOutcome.Missing)
        {
            return Missing(id);
        }

        if (outcome == ArtifactPutOutcome.TooLarge)
        {
            metrics.Add(kindText, "rejected");
            return TooLarge();
        }

        if (outcome == ArtifactPutOutcome.Quota)
        {
            metrics.Add(kindText, "rejected");
            return WebProblems.Of(StatusCodes.Status413RequestEntityTooLarge, "quota_exceeded", "Quota exceeded.", "The account artifact quota was exceeded.");
        }

        if (outcome == ArtifactPutOutcome.Conflict)
        {
            metrics.Add(kindText, "conflict");
            await audit.WriteAsync("artifact.updated", user.Name, id, hash, [], cancellationToken).ConfigureAwait(false);
            return WebProblems.Of(StatusCodes.Status409Conflict, "divergence", "Divergent version.", "Different bytes already exist for this version number.");
        }

        metrics.Add(kindText, outcome == ArtifactPutOutcome.Created ? "created" : "updated");
        await audit.WriteAsync(outcome == ArtifactPutOutcome.Created ? "artifact.created" : "artifact.updated", user.Name, id, hash, [], cancellationToken).ConfigureAwait(false);
        if (outcome == ArtifactPutOutcome.SameHash)
        {
            context.Response.Headers[ArtifactWireContract.SameHashHeader] = "true";
            return Results.StatusCode(StatusCodes.Status200OK);
        }

        return outcome == ArtifactPutOutcome.Created ? Results.StatusCode(StatusCodes.Status201Created) : Results.StatusCode(StatusCodes.Status200OK);
    }

    private static async Task<IResult> DeleteAsync(string id, IUserContext user, ArtifactAccountDirectory directory, CancellationToken cancellationToken)
    {
        var deleted = await directory.DeleteAsync(user.Name, id, cancellationToken).ConfigureAwait(false);
        return deleted ? Results.NoContent() : Missing(id);
    }

    private static async Task<IResult> IssueTicketAsync(string id, int n, HttpContext context, IUserContext user, ArtifactAccountDirectory directory, ArtifactRenderTickets tickets, CancellationToken cancellationToken)
    {
        var version = await directory.VersionAsync(user.Name, id, n, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Missing(id);
        }

        var ticket = tickets.Issue(user.Name, id, n, version.ContentSha256);
        var url = "/cowork/artifact-render/" + ticket;
        return Results.Json(new RenderTicketDocument(url), ArtifactsJsonContext.Default.RenderTicketDocument);
    }

    private static async Task<IResult> GetGrantsAsync(string id, int n, IUserContext user, ArtifactAccountDirectory directory, ArtifactGrants grants, CancellationToken cancellationToken)
    {
        var version = await directory.VersionAsync(user.Name, id, n, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Missing(id);
        }

        var rows = grants.List(user.Name, id).Where(grant => grant.Number == n).ToArray();
        return Results.Json(rows, ArtifactsJsonContext.Default.ListArtifactGrantDocument);
    }

    private static async Task<IResult> PutGrantAsync(
        string id,
        int n,
        HttpContext context,
        IUserContext user,
        ArtifactAccountDirectory directory,
        ArtifactGrants grants,
        ArtifactMetrics metrics,
        ArtifactAudit audit,
        CancellationToken cancellationToken)
    {
        var version = await directory.VersionAsync(user.Name, id, n, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Missing(id);
        }

        if (string.Equals(context.Request.Headers["X-Artifact-Grant"], "revoke", StringComparison.Ordinal))
        {
            var revoked = grants.Revoke(user.Name, id, n, context.Request.Headers["X-Artifact-Scope"].ToString() is { Length: > 0 } scope ? scope : null, null);
            metrics.Add(version.Kind, "updated");
            await audit.WriteAsync("artifact.revoke", user.Name, id, version.ContentSha256, revoked, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }

        var document = await context.Request.ReadFromJsonAsync(ArtifactsJsonContext.Default.ArtifactGrantDocument, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.ContentSha256, version.ContentSha256, StringComparison.Ordinal) || document.Number != n)
        {
            metrics.Add(version.Kind, "rejected");
            return WebProblems.Of(StatusCodes.Status409Conflict, "grant_mismatch", "Grant mismatch.", "A grant must bind the current version hash.");
        }

        if (document.Origins.Any(static origin => !ArtifactOriginRules.IsAllowed(origin)))
        {
            metrics.Add(version.Kind, "rejected");
            return WebProblems.Of(StatusCodes.Status400BadRequest, "origin_rejected", "Origin rejected.", "An origin was rejected.");
        }

        var bound = document with { ArtifactId = id, Number = n, GrantedAt = DateTimeOffset.UtcNow };
        grants.Save(user.Name, bound);
        metrics.Add(version.Kind, "updated");
        await audit.WriteAsync("artifact.grant", user.Name, id, version.ContentSha256, bound.Origins, cancellationToken).ConfigureAwait(false);
        return Results.Json(bound, ArtifactsJsonContext.Default.ArtifactGrantDocument);
    }

    private static async Task<IResult> RenderAsync(
        string ticket,
        HttpContext context,
        ArtifactRenderTickets tickets,
        ArtifactAccountDirectory directory,
        ArtifactGrants grants,
        IOptions<Autonogrammer.Api.Options.ApiOptions> api,
        CancellationToken cancellationToken)
    {
        if (!tickets.TryOpen(ticket, out var account, out var artifactId, out var version, out var hash))
        {
            return Missing(artifactId.Length == 0 ? "ticket" : artifactId);
        }

        var stored = await directory.VersionAsync(account, artifactId, version, cancellationToken).ConfigureAwait(false);
        if (stored is null || !string.Equals(stored.ContentSha256, hash, StringComparison.Ordinal))
        {
            return Missing(artifactId);
        }

        _ = grants.List(account, artifactId).Where(grant => grant.Number == version && string.Equals(grant.ContentSha256, hash, StringComparison.Ordinal)).ToArray();
        var bytes = await directory.ContentAsync(account, artifactId, version, cancellationToken).ConfigureAwait(false);
        var html = HtmlDocumentWrapper.Wrap(Encoding.UTF8.GetString(bytes ?? []), scripts: true);
        foreach (var (name, value) in ArtifactDocumentPolicy.RenderResponseHeaders(api.Value.Cors.Origins))
        {
            context.Response.Headers[name] = value;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        return Results.Text(html, "text/html; charset=utf-8", Encoding.UTF8);
    }

    private readonly record struct VersionBody(byte[] Bytes, IResult? Error);

    private static async Task<VersionBody> ReadVersionBodyAsync(HttpContext context, int ceiling, string kindText, ArtifactMetrics metrics, ArtifactAudit audit, string owner, string id, CancellationToken cancellationToken)
    {
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = ceiling;
        }

        if (context.Request.ContentLength is long declaredLength && declaredLength > ArtifactWireContract.MaxContentBytes)
        {
            metrics.Add(string.IsNullOrEmpty(kindText) ? "unknown" : kindText, "rejected");
            return new VersionBody([], declaredLength > ceiling ? TooLarge() : QuotaExceeded());
        }

        CappedRead read;
        try
        {
            read = await ReadCappedAsync(context.Request.Body, ceiling, cancellationToken).ConfigureAwait(false);
        }
        catch (BadHttpRequestException)
        {
            metrics.Add(string.IsNullOrEmpty(kindText) ? "unknown" : kindText, "rejected");
            return new VersionBody([], TooLarge());
        }

        if (read.TooLarge)
        {
            metrics.Add(string.IsNullOrEmpty(kindText) ? "unknown" : kindText, "rejected");
            await audit.WriteAsync("artifact.updated", owner, id, null, [], cancellationToken).ConfigureAwait(false);
            return new VersionBody([], TooLarge());
        }

        if (read.Bytes.Length > ArtifactWireContract.MaxContentBytes)
        {
            metrics.Add(string.IsNullOrEmpty(kindText) ? "unknown" : kindText, "rejected");
            return new VersionBody([], QuotaExceeded());
        }

        return new VersionBody(read.Bytes, null);
    }


    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult QuotaExceeded() =>
        WebProblems.Of(StatusCodes.Status413RequestEntityTooLarge, "quota_exceeded", "Quota exceeded.", "The account artifact quota was exceeded.");


    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Missing(string id) =>
        WebProblems.Of(StatusCodes.Status404NotFound, "not_found", "Artifact not found.", "No artifact has id '" + id + "'.");

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult TooLarge() =>
        WebProblems.Of(StatusCodes.Status413RequestEntityTooLarge, "too_large", "Artifact too large.", "The body exceeds the artifact ceiling.");

    private static async Task<CappedRead> ReadCappedAsync(Stream body, int ceiling, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            using var stream = new MemoryStream();
            while (true)
            {
                var read = await body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return new CappedRead(stream.ToArray(), false);
                }

                if (stream.Length + read > ceiling)
                {
                    while (await body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false) > 0)
                    {
                    }

                    return new CappedRead([], true);
                }

                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private readonly record struct CappedRead(byte[] Bytes, bool TooLarge);
}
