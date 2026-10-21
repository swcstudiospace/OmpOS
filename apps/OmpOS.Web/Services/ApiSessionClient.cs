using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Sessions;

namespace OmpOS.Web.Services;

/// <summary>
/// Typed client for the session endpoints (<c>POST/GET /sessions</c>,
/// <c>GET /sessions/{id}</c>, <c>POST /sessions/{id}/prompts</c>).
/// The event stream itself lives in <see cref="SseEventStream"/>.
/// </summary>
internal sealed class ApiSessionClient
{
    private readonly HttpClient _http;

    /// <summary>Initializes a new instance of the <see cref="ApiSessionClient"/> class.</summary>
    /// <param name="http">An HTTP client whose base address is the API origin.</param>
    internal ApiSessionClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <summary>Creates a session for the given owner.</summary>
    /// <param name="ownerId">The reserved owner identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created session.</returns>
    internal async Task<Session> CreateSessionAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        using var response = await _http.PostAsJsonAsync(
            new Uri("sessions", UriKind.Relative),
            new CreateSessionRequest { OwnerId = ownerId },
            WebJsonContext.Default.CreateSessionRequest,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var session = await response.Content.ReadFromJsonAsync(
            OmpOsJsonContext.Default.Session,
            cancellationToken).ConfigureAwait(false);
        return session ?? throw new InvalidOperationException("The API returned an empty session body.");
    }

    /// <summary>Lists all sessions visible to the caller.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session list.</returns>
    internal async Task<IReadOnlyList<Session>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(new Uri("sessions", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var sessions = await response.Content.ReadFromJsonAsync(
            OmpOsJsonContext.Default.SessionArray,
            cancellationToken).ConfigureAwait(false);
        return sessions ?? [];
    }

    /// <summary>Fetches one session, including its stream-event log.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session, or null when the API reports 404.</returns>
    internal async Task<Session?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        using var response = await _http.GetAsync(
            new Uri($"sessions/{Uri.EscapeDataString(sessionId)}", UriKind.Relative),
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync(
            OmpOsJsonContext.Default.Session,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Submits a prompt to a session (202 when accepted).</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="prompt">The user prompt text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task SubmitPromptAsync(string sessionId, string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        using var response = await _http.PostAsJsonAsync(
            new Uri($"sessions/{Uri.EscapeDataString(sessionId)}/prompts", UriKind.Relative),
            new SubmitPromptRequest { Prompt = prompt },
            WebJsonContext.Default.SubmitPromptRequest,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The error body is best-effort context on an already-failed call; any read failure keeps the status-only exception.")]
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            body = null;
        }

        throw new ApiClientException(response.StatusCode, body);
    }
}
