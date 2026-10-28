using System.Net;
using System.Text.Json;
using Autonogrammer.Presentation.Cloud;

namespace Autonogrammer.Native.Cloud;

/// <summary>
/// Turns a failed control-plane response into the <see cref="CloudHostException"/> the Cloud agents page shows. The message is the problem's own
/// <c>detail</c>, which the control plane writes without a prompt, a secret or a provider response; when there is none, a fixed sentence by kind is
/// used. The request URL, the token and the body are never put in a message.
/// </summary>
internal static class ApiCloudFailures
{
    private const int MaxMessageChars = 300;
    private const int MaxBodyBytes = 64 * 1024;
    private const string ProblemPrefix = "/problems/";

    /// <summary>Maps a response status and problem code to the kind of failure.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="code">The problem code, or null.</param>
    /// <returns>The failure kind.</returns>
    public static CloudFailureKind KindOf(HttpStatusCode status, string? code) => (int)status switch
    {
        401 or 403 => CloudFailureKind.Unauthorized,
        404 => CloudFailureKind.NotFound,
        409 => CloudFailureKind.Conflict,
        400 or 413 or 415 or 422 => CloudFailureKind.Rejected,
        429 => string.Equals(code, "compute_limit_reached", StringComparison.Ordinal) ? CloudFailureKind.QuotaExceeded : CloudFailureKind.LimitReached,
        _ => CloudFailureKind.Unavailable,
    };

    /// <summary>Builds the exception for a response that is not a success.</summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">Cancels the read of the problem body.</param>
    /// <returns>The exception to throw.</returns>
    public static async Task<CloudHostException> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (code, detail) = await ReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
        var kind = KindOf(response.StatusCode, code);
        return new CloudHostException(kind, detail ?? FallbackMessage(kind));
    }

    /// <summary>Gets the sentence shown for a failure that has no problem detail.</summary>
    /// <param name="kind">The failure kind.</param>
    /// <returns>A plain sentence.</returns>
    public static string FallbackMessage(CloudFailureKind kind) => kind switch
    {
        CloudFailureKind.NotConfigured => "Cloud agents are not set up on this installation.",
        CloudFailureKind.Unauthorized => "Sign in to the Autonogrammer service to use cloud agents.",
        CloudFailureKind.LimitReached => "The cloud agent limit is reached.",
        CloudFailureKind.QuotaExceeded => "The compute budget of this agent is used up.",
        CloudFailureKind.NotFound => "That cloud agent does not exist.",
        CloudFailureKind.Conflict => "The cloud agent is in a state that does not allow this.",
        CloudFailureKind.Rejected => "The Autonogrammer service refused the request.",
        _ => "The Autonogrammer service could not be reached or reported an error. Try again.",
    };

    private static async Task<(string? Code, string? Detail)> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var limited = new MemoryStream();
                var buffer = new byte[4096];
                int read;
                while (limited.Length < MaxBodyBytes && (read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    limited.Write(buffer.AsSpan(0, read));
                }

                using var document = JsonDocument.Parse(limited.ToArray());
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return (null, null);
                }

                return (CodeOf(document.RootElement), DetailOf(document.RootElement));
            }
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or IOException or InvalidOperationException)
        {
            // A body that is not a problem document is no reason to hide the status: the kind falls back to a fixed sentence.
            _ = ex;
            return (null, null);
        }
    }

    private static string? CodeOf(JsonElement problem)
    {
        if (!problem.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = type.GetString();
        var index = value?.LastIndexOf(ProblemPrefix, StringComparison.Ordinal) ?? -1;
        return index < 0 ? null : value![(index + ProblemPrefix.Length)..];
    }

    private static string? DetailOf(JsonElement problem)
    {
        foreach (var name in (ReadOnlySpan<string>)["detail", "message", "title"])
        {
            if (problem.TryGetProperty(name, out var member) && member.ValueKind == JsonValueKind.String && member.GetString() is { } text && !string.IsNullOrWhiteSpace(text))
            {
                var plain = new string([.. text.Where(static c => !char.IsControl(c))]).Trim();
                if (plain.Length > 0)
                {
                    return plain.Length <= MaxMessageChars ? plain : plain[..MaxMessageChars];
                }
            }
        }

        return null;
    }
}
