using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using OmpOS.Api.Endpoints;
using OmpOS.Api.Options;

namespace OmpOS.Api.Services;

/// <summary>Reads a JSON request body with the same rules as the prompt endpoints: 415 for another media type, 413 above the size limit, 400 for malformed JSON.</summary>
internal static class WebBodyReader
{
    /// <summary>Reads and parses a body.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="context">The request.</param>
    /// <param name="options">The API limits.</param>
    /// <param name="typeInfo">The source-generated type metadata.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The value, or the problem that rejected the body.</returns>
    public static async Task<BodyOutcome<T>> ReadAsync<T>(HttpContext context, ApiOptions options, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeInfo);
        if (!IsJson(context.Request.ContentType))
        {
            return new BodyOutcome<T>(null, SessionEndpoints.ProblemResponse(415, "Unsupported media type.", "Content-Type must be application/json."));
        }

        if (context.Request.ContentLength is { } declared && declared > options.MaxRequestBodyBytes)
        {
            return new BodyOutcome<T>(null, SessionEndpoints.ProblemResponse(413, "Request body too large.", "The request body exceeds the configured limit."));
        }

        using var destination = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > options.MaxRequestBodyBytes)
            {
                return new BodyOutcome<T>(null, SessionEndpoints.ProblemResponse(413, "Request body too large.", "The request body exceeds the configured limit."));
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        if (total == 0)
        {
            return new BodyOutcome<T>(null, SessionEndpoints.ProblemResponse(400, "Invalid request body.", "The request body must be a JSON object."));
        }

        try
        {
            var value = JsonSerializer.Deserialize(destination.ToArray(), typeInfo);
            return value is null
                ? new BodyOutcome<T>(null, SessionEndpoints.ProblemResponse(400, "Invalid request body.", "The request body must be a JSON object."))
                : new BodyOutcome<T>(value, null);
        }
        catch (JsonException)
        {
            return new BodyOutcome<T>(null, SessionEndpoints.ProblemResponse(400, "Invalid request body.", "The request body is not valid JSON or has an unknown value."));
        }
    }

    private static bool IsJson(string? contentType)
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
}
