using System.Globalization;
using System.Net;
using System.Text.Json;

namespace OmpOS.Harness.Providers;

/// <summary>
/// Checks whether a local OpenAI-compatible server is listening at a catalogue entry's endpoint. It sends one unauthenticated
/// GET to the model list and reads the ids from the answer. The probe holds no key and sends none; a server that wants a key
/// is reported as running, not as missing. The probe never throws for a server that is down, so the settings view can show
/// a not-running error instead of an exception.
/// </summary>
public sealed class LocalProviderProbe
{
    /// <summary>How long the probe waits for an answer when the caller gives no timeout.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private const int MaxBodyBytes = 1024 * 1024;

    private readonly HttpClient _client;
    private readonly TimeProvider _time;
    private readonly TimeSpan _timeout;

    /// <summary>Creates a probe that sends its requests through <paramref name="client"/>.</summary>
    /// <param name="client">The client to send through. The probe does not dispose it.</param>
    /// <param name="time">The clock that measures the timeout.</param>
    /// <param name="timeout">How long to wait for an answer, or null for <see cref="DefaultTimeout"/>.</param>
    public LocalProviderProbe(HttpClient client, TimeProvider time, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(time);
        if (timeout is { } requested)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requested, TimeSpan.Zero, nameof(timeout));
        }

        _client = client;
        _time = time;
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>Returns the model-list address the probe requests for an OpenAI-compatible base endpoint.</summary>
    /// <param name="baseEndpoint">The entry's base endpoint.</param>
    public static Uri ModelsUri(Uri baseEndpoint)
    {
        ArgumentNullException.ThrowIfNull(baseEndpoint);
        var root = baseEndpoint.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var tail = root.EndsWith("/v1", StringComparison.Ordinal) ? "/models" : "/v1/models";
        return new Uri(root + tail, UriKind.Absolute);
    }

    /// <summary>Probes a local OpenAI-compatible entry.</summary>
    /// <param name="entry">The entry to probe. It must be a local entry that speaks the OpenAI-compatible protocol.</param>
    /// <param name="cancellationToken">Cancels the probe. A cancelled probe throws; it is not reported as not running.</param>
    /// <exception cref="ArgumentException">The entry is not a local OpenAI-compatible entry.</exception>
    /// <returns>What the probe found.</returns>
    public async Task<ProviderProbeResult> ProbeAsync(ProviderEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Kind != ProviderKind.Local || entry.Transport != ProviderTransport.OpenAiCompatible || entry.BaseEndpoint is null)
        {
            throw new ArgumentException("Only a local OpenAI-compatible entry with a base endpoint can be probed.", nameof(entry));
        }

        var uri = ModelsUri(entry.BaseEndpoint);
        using var timeout = new CancellationTokenSource(_timeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);
            return await ReadAsync(entry, uri, response, linked.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return NotRunning(entry, uri);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Only the probe's own timeout fired, so the endpoint did not answer in time.
            return NotRunning(entry, uri);
        }
    }

    private static async Task<ProviderProbeResult> ReadAsync(
        ProviderEntry entry,
        Uri uri,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var code = (int)response.StatusCode;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new ProviderProbeResult(
                ProviderProbeStatus.RunningAuthRequired,
                uri,
                code,
                [],
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{entry.Id} is running at {uri} but refused the request (HTTP {code}). Enter the API key that the server is configured with."));
        }

        if (!response.IsSuccessStatusCode)
        {
            return Unexpected(entry, uri, code, string.Create(CultureInfo.InvariantCulture, $"answered HTTP {code}"));
        }

        var ids = await ReadModelIdsAsync(response, cancellationToken).ConfigureAwait(false);
        if (ids is null)
        {
            return Unexpected(entry, uri, code, "did not answer with an OpenAI-style model list");
        }

        return new ProviderProbeResult(
            ProviderProbeStatus.Running,
            uri,
            code,
            ids,
            string.Create(CultureInfo.InvariantCulture, $"{entry.Id} is running at {uri} and lists {ids.Count} model(s)."));
    }

    private static async Task<IReadOnlyList<string>?> ReadModelIdsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaxBodyBytes)
                {
                    return null;
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            buffer.Position = 0;
            return ParseModelIds(buffer);
        }
    }

    private static List<string>? ParseModelIds(MemoryStream body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var ids = new List<string>();
            foreach (var model in data.EnumerateArray())
            {
                if (model.ValueKind == JsonValueKind.Object
                    && model.TryGetProperty("id", out var id)
                    && id.ValueKind == JsonValueKind.String
                    && id.GetString() is { Length: > 0 } text)
                {
                    ids.Add(text);
                }
            }

            return ids;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ProviderProbeResult NotRunning(ProviderEntry entry, Uri uri) =>
        new(
            ProviderProbeStatus.NotRunning,
            uri,
            null,
            [],
            string.Create(
                CultureInfo.InvariantCulture,
                $"{entry.Id} is not running: nothing answered at {uri}. Start the server, or correct the port, and try again."));

    private static ProviderProbeResult Unexpected(ProviderEntry entry, Uri uri, int code, string what) =>
        new(
            ProviderProbeStatus.UnexpectedResponse,
            uri,
            code,
            [],
            string.Create(
                CultureInfo.InvariantCulture,
                $"Something answers at {uri} but it {what}. Check that {entry.Id} is the server that owns this port."));
}
