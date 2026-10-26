using System.Net;
using System.Net.Http.Json;
using System.Text;
using OmpOS.Contracts.Runs;
using OmpOS.Contracts.Serialization;
using Shouldly;

namespace OmpOS.Api.IntegrationTests;

/// <summary>Shared HTTP helpers for hosted run tests: create, read, read output, cancel, and bounded waits.</summary>
internal static class CloudRunClient
{
    /// <summary>The longest a test waits for a signal from the host.</summary>
    public static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Creates a run on a session and returns the raw response.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>The HTTP response (caller disposes).</returns>
    public static async Task<HttpResponseMessage> PostRunAsync(HttpClient client, string sessionId, string prompt, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(prompt);
        using var content = new StringContent($"{{\"prompt\":\"{prompt}\"}}", Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri($"/sessions/{sessionId}/runs", UriKind.Relative), content, testToken).ConfigureAwait(false);
    }

    /// <summary>Creates a run, expects 202 with the run's Location, and returns the accepted run.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>The accepted run.</returns>
    public static async Task<Run> CreateRunAsync(HttpClient client, string sessionId, string prompt, CancellationToken testToken)
    {
        using var response = await PostRunAsync(client, sessionId, prompt, testToken).ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var run = await ReadRunAsync(response, testToken).ConfigureAwait(false);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"/sessions/{sessionId}/runs/{run.Id}");
        return run;
    }

    /// <summary>Reads a run's status, expecting 200.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>The run.</returns>
    public static async Task<Run> GetRunAsync(HttpClient client, string sessionId, string runId, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var response = await client.GetAsync(new Uri($"/sessions/{sessionId}/runs/{runId}", UriKind.Relative), testToken).ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ReadRunAsync(response, testToken).ConfigureAwait(false);
    }

    /// <summary>Reads a run's output, expecting 200.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>The run output.</returns>
    public static async Task<RunOutput> GetOutputAsync(HttpClient client, string sessionId, string runId, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var response = await client.GetAsync(new Uri($"/sessions/{sessionId}/runs/{runId}/output", UriKind.Relative), testToken).ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync(OmpOsJsonContext.Default.RunOutput, testToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Run output response body was empty.");
    }

    /// <summary>Sends a cancel request and returns the raw response.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>The HTTP response (caller disposes).</returns>
    public static async Task<HttpResponseMessage> PostCancelAsync(HttpClient client, string sessionId, string runId, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var content = new StringContent(string.Empty);
        return await client.PostAsync(new Uri($"/sessions/{sessionId}/runs/{runId}/cancel", UriKind.Relative), content, testToken).ConfigureAwait(false);
    }

    /// <summary>Reads the run in a 2xx response body.</summary>
    /// <param name="response">The response.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>The run.</returns>
    public static async Task<Run> ReadRunAsync(HttpResponseMessage response, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        return await response.Content.ReadFromJsonAsync(OmpOsJsonContext.Default.Run, testToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Run response body was empty.");
    }

    /// <summary>Waits for a signal from the host, failing fast when it does not arrive.</summary>
    /// <param name="signal">The signal task.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>A task that completes when the signal does.</returns>
    public static Task AwaitAsync(Task signal, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return signal.WaitAsync(SignalTimeout, testToken);
    }
}
