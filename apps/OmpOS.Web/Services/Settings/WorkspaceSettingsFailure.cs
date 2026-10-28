using System.Net;
using System.Text.Json;

namespace OmpOS.Web.Services.Settings;

/// <summary>Tells the expected failures of the settings endpoints from defects, and words them for a settings page. The words never quote a response body.</summary>
internal static class WorkspaceSettingsFailure
{
    /// <summary>Returns whether an exception is an expected failure of a settings read: a refused call, a network fault or a body that is not the expected JSON.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>True for an expected failure.</returns>
    internal static bool IsExpected(Exception exception) =>
        exception is ApiClientException or HttpRequestException or JsonException;

    /// <summary>Words a failure as one sentence for the page.</summary>
    /// <param name="exception">An exception for which <see cref="IsExpected"/> is true.</param>
    /// <returns>The sentence.</returns>
    internal static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is ApiClientException refused && refused.StatusCode != default
            ? refused.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Sign in again: the server did not accept the session.",
                HttpStatusCode.NotFound => "This workspace is not available to your account.",
                HttpStatusCode.TooManyRequests => "Too many requests. Wait a minute and try again.",
                _ => $"The server could not answer (status {(int)refused.StatusCode}).",
            }
            : "The server could not be reached or sent an answer this page cannot read.";
    }
}
