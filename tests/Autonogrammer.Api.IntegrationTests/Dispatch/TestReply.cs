using System.Net;
using System.Text.Json;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>A reply read in full: the status, the body text, and the Retry-After value when the reply sets one.</summary>
/// <param name="Status">The status code.</param>
/// <param name="Body">The body text.</param>
/// <param name="RetryAfter">The Retry-After value, when set.</param>
internal sealed record TestReply(HttpStatusCode Status, string Body, string? RetryAfter)
{
    /// <summary>Gets the problem type (<c>/problems/{code}</c>) of an error reply.</summary>
    /// <returns>The type, or null when the body carries none.</returns>
    public string? ProblemType()
    {
        using var document = JsonDocument.Parse(Body);
        return document.RootElement.TryGetProperty("type", out var type) ? type.GetString() : null;
    }
}
