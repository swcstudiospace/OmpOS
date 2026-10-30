using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Contracts.Sessions;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Shared HTTP helpers for session tests: creation, prompt submission, reads, waits, and SSE parsing.
/// Stream waits link the test cancellation token with an internal timeout so hangs fail fast.
/// </summary>
internal static class SessionClient
{
    private static readonly TimeSpan StreamTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Creates a session and returns its identifier.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <param name="json">The raw JSON body.</param>
    /// <returns>The created session identifier.</returns>
    public static async Task<string> CreateSessionAsync(HttpClient client, CancellationToken testToken, string json = "{}")
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(json);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/sessions", UriKind.Relative), content, testToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Session creation response has no id.");
    }

    /// <summary>Submits a prompt and returns the raw response for status assertions.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="json">The raw JSON body.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <param name="contentType">The content type header.</param>
    /// <param name="idempotencyKey">The Idempotency-Key header value, or null to send none.</param>
    /// <returns>The HTTP response (caller disposes).</returns>
    public static async Task<HttpResponseMessage> PostPromptAsync(
        HttpClient client,
        string sessionId,
        string json,
        CancellationToken testToken,
        string contentType = "application/json",
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(json);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/sessions/{sessionId}/prompts", UriKind.Relative))
        {
            Content = new StringContent(json, Encoding.UTF8, contentType),
        };
        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request, testToken).ConfigureAwait(true);
    }

    /// <summary>Reads a session, throwing when the response is not successful.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>The session.</returns>
    public static async Task<Session> ReadSessionAsync(HttpClient client, string sessionId, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(sessionId);
        using var response = await client.GetAsync(new Uri($"/sessions/{sessionId}", UriKind.Relative), testToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(AutonogrammerJsonContext.Default.Session, testToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Session response body was empty.");
    }

    /// <summary>Polls a session until it reaches the expected status or the timeout elapses.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="timeout">The maximum wait.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>The session at the expected status.</returns>
    public static async Task<Session> WaitForStatusAsync(
        HttpClient client,
        string sessionId,
        SessionStatus status,
        TimeSpan timeout,
        CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(sessionId);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, testToken);
        var cancellationToken = linked.Token;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var session = await ReadSessionAsync(client, sessionId, cancellationToken).ConfigureAwait(false);
            if (session.Status == status)
            {
                return session;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reads up to <paramref name="maxItems"/> SSE items, then stops (disconnecting).</summary>
    /// <param name="client">The test client.</param>
    /// <param name="path">The stream path.</param>
    /// <param name="maxItems">The maximum items to read.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <param name="lastEventId">Optional Last-Event-ID resume header.</param>
    /// <returns>The items read before stopping.</returns>
    public static async Task<IReadOnlyList<SseItem<string>>> ReadStreamAsync(
        HttpClient client,
        string path,
        int maxItems,
        CancellationToken testToken,
        string? lastEventId = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(path);
        using var timeoutSource = new CancellationTokenSource(StreamTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, testToken);
        var cancellationToken = linked.Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (lastEventId is not null)
        {
            request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var parser = SseParser.Create(stream);
            var items = new List<SseItem<string>>();
            await foreach (var item in parser.EnumerateAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(item);
                if (items.Count >= maxItems)
                {
                    break;
                }
            }

            return items;
        }
    }
}
