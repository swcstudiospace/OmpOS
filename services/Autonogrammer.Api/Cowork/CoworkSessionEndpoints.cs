using System.Text.Json;
using Autonogrammer.Api.Endpoints;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Contracts.Cowork;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork;

/// <summary>
/// Cowork summary routes. The surface is the route, never a body field. The owner is the token, never a body field.
/// An absent row and another owner's row share one 404.
/// </summary>
internal static class CoworkSessionEndpoints
{
    internal sealed class Mapped
    {
        private int _mapped;

        public bool TryMark() => Interlocked.Exchange(ref _mapped, 1) == 0;
    }

    /// <summary>Maps <c>PUT /cowork/sessions/{id}</c> and <c>DELETE /cowork/sessions/{id}</c>, once per host.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapCoworkSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (!endpoints.ServiceProvider.GetRequiredService<Mapped>().TryMark())
        {
            return endpoints;
        }

        endpoints.MapPut("/cowork/sessions/{id}", PutAsync)
            .WithName("PutCoworkSession")
            .WithTags("Cowork")
            .WithSummary("Stores a Cowork summary. The owner is the token and the surface is this route.")
            .Accepts<CoworkSessionSummary>("application/json")
            .Produces<CoworkSessionSummary>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(CoworkSessionRateLimiterPolicy.PolicyName);
        endpoints.MapDelete("/cowork/sessions/{id}", DeleteAsync)
            .WithName("DeleteCoworkSession")
            .WithTags("Cowork")
            .WithSummary("Removes a Cowork summary. Another owner's row reads as absent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(CoworkSessionRateLimiterPolicy.PolicyName);
        return endpoints;
    }

    private static async Task<Results<Ok<CoworkSessionSummary>, ProblemHttpResult>> PutAsync(
        HttpContext context,
        string id,
        CoworkSessionService service,
        IUserContext user,
        IOptions<ApiOptions> options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return SessionEndpoints.ProblemResponse(400, "Invalid session id.", "The session id must not be empty.");
        }

        var read = await ReadSummaryAsync(context, options.Value.MaxRequestBodyBytes, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (!string.Equals(read.Summary!.Id, id, StringComparison.Ordinal))
        {
            return SessionEndpoints.ProblemResponse(400, "Invalid session id.", "The session id in the body must match the route.");
        }

        var (outcome, stored) = await service.PutAsync(id, read.Summary, user.Name, cancellationToken).ConfigureAwait(false);
        return outcome switch
        {
            CoworkPutOutcome.NotFound => SessionEndpoints.SessionNotFound(id),
            CoworkPutOutcome.Conflict => SessionEndpoints.ProblemResponse(409, "Cowork session cap reached.", "This owner already holds the maximum number of Cowork sessions."),
            _ => TypedResults.Ok(stored!),
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        string id,
        CoworkSessionService service,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var removed = await service.DeleteAsync(id, user.Name, cancellationToken).ConfigureAwait(false);
        return removed ? TypedResults.NoContent() : SessionEndpoints.SessionNotFound(id);
    }

    private static async Task<SummaryRead> ReadSummaryAsync(HttpContext context, long limit, CancellationToken cancellationToken)
    {
        var body = await ReadLimitedAsync(context, limit, cancellationToken).ConfigureAwait(false);
        if (body.TooLarge)
        {
            return new SummaryRead(null, SessionEndpoints.ProblemResponse(413, "Request body too large.", "The request body exceeds the configured limit."));
        }

        if (body.Bytes.Length == 0)
        {
            return new SummaryRead(null, SessionEndpoints.ProblemResponse(400, "Invalid request body.", "The request body is not valid JSON."));
        }

        if (!IsJsonContentType(context.Request.ContentType))
        {
            return new SummaryRead(null, SessionEndpoints.ProblemResponse(415, "Unsupported media type.", "Content-Type must be application/json."));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body.Bytes);
        }
        catch (JsonException)
        {
            return new SummaryRead(null, SessionEndpoints.ProblemResponse(400, "Invalid request body.", "The request body is not valid JSON."));
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new SummaryRead(null, SessionEndpoints.ProblemResponse(400, "Invalid request body.", "The request body is not valid JSON."));
            }

            if (NamesOwner(document.RootElement))
            {
                return new SummaryRead(null, SessionEndpoints.ProblemResponse(400, "Owner not accepted.", "The owner is the authenticated caller. The request body must not name an owner."));
            }

            if (NamesSurface(document.RootElement))
            {
                return new SummaryRead(null, SessionEndpoints.ProblemResponse(400, "Surface not accepted.", "The surface is fixed by the route. The request body must not name a surface."));
            }
        }

        try
        {
            var summary = JsonSerializer.Deserialize(body.Bytes, CoworkJsonContext.Default.CoworkSessionSummary);
            return summary is null
                ? new SummaryRead(null, SessionEndpoints.ProblemResponse(400, "Invalid request body.", "The request body is not valid JSON."))
                : new SummaryRead(summary, null);
        }
        catch (JsonException)
        {
            return new SummaryRead(null, SessionEndpoints.ProblemResponse(400, "Invalid request body.", "The request body is not valid JSON."));
        }
    }

    private sealed record SummaryRead(CoworkSessionSummary? Summary, ProblemHttpResult? Error);

    private static bool NamesOwner(JsonElement body)
    {
        foreach (var property in body.EnumerateObject())
        {
            if (property.Name.Equals("ownerId", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("owner", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NamesSurface(JsonElement body)
    {
        foreach (var property in body.EnumerateObject())
        {
            if (property.Name.Equals("surface", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsJsonContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || (mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase)
                && mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<BodyRead> ReadLimitedAsync(HttpContext context, long limit, CancellationToken cancellationToken)
    {
        if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > limit)
        {
            return new BodyRead(true, []);
        }

        using var destination = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > limit)
            {
                return new BodyRead(true, []);
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return new BodyRead(false, destination.ToArray());
    }

    private sealed record BodyRead(bool TooLarge, byte[] Bytes);
}
