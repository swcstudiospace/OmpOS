using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Autonogrammer.Web.Services;

/// <summary>Turns a non-success response into an <see cref="ApiProblemException"/>.</summary>
internal static class ApiProblems
{
    private const string TypePrefix = "/problems/";

    /// <summary>Does nothing for a success status; otherwise reads the bounded problem body and throws.</summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">Cancels the read of the body.</param>
    /// <returns>A task that completes for a success status.</returns>
    /// <exception cref="ApiProblemException">The status is not a success.</exception>
    internal static async Task ThrowIfNotSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiProblemBody? body = null;
        try
        {
            var text = await BoundedContent.ReadTextAsync(response.Content, BoundedContent.MaxErrorBytes, cancellationToken).ConfigureAwait(false);
            body = JsonSerializer.Deserialize(text, WebJsonContext.Default.ApiProblemBody);
        }
        catch (JsonException)
        {
            // The body is best-effort context on an already-failed call: the status alone still maps to a kind.
        }
        catch (ApiClientException)
        {
            // An error body over the bound is dropped for the same reason.
        }
        catch (HttpRequestException)
        {
            // The connection ended while the body was read.
        }

        throw Create(response.StatusCode, body, RetryAfterOf(response));
    }

    /// <summary>Builds the exception for a status and an optional parsed body.</summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="body">The parsed problem body, or null when there was none.</param>
    /// <param name="retryAfter">The <c>Retry-After</c> delay, or null.</param>
    /// <returns>The exception.</returns>
    internal static ApiProblemException Create(HttpStatusCode statusCode, ApiProblemBody? body, TimeSpan? retryAfter)
    {
        var title = string.IsNullOrWhiteSpace(body?.Title) ? DefaultTitle(statusCode) : body.Title;
        return new ApiProblemException(
            ApiProblemException.KindOf(statusCode),
            statusCode,
            CodeOf(body?.Type),
            title,
            string.IsNullOrWhiteSpace(body?.Detail) ? null : body.Detail,
            retryAfter);
    }

    /// <summary>Reads the problem code from a problem <c>type</c> such as <c>/problems/session_closed</c>.</summary>
    /// <param name="type">The type member, or null.</param>
    /// <returns>The code, or null when the type has another form.</returns>
    internal static string? CodeOf(string? type)
    {
        if (type is null || !type.StartsWith(TypePrefix, StringComparison.Ordinal) || type.Length == TypePrefix.Length)
        {
            return null;
        }

        var code = type[TypePrefix.Length..];
        return code.Length <= 64 && code.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? code : null;
    }

    private static TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta;
        }

        return header?.Date is { } date ? date - DateTimeOffset.UtcNow : null;
    }

    private static string DefaultTitle(HttpStatusCode statusCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)statusCode} {statusCode}");
}
