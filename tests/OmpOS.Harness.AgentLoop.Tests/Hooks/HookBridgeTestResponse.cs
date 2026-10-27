using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>An HTTP answer read from a connection to the bridge: status, headers and body. A connection closed without an answer reads as status 0.</summary>
internal sealed class HookBridgeTestResponse
{
    private static readonly TimeSpan SafetyLimit = TimeSpan.FromSeconds(30);

    private HookBridgeTestResponse(int status, IReadOnlyDictionary<string, string> headers, string body)
    {
        Status = status;
        Headers = headers;
        Body = body;
    }

    /// <summary>Gets the HTTP status, or 0 when the server closed the connection without an answer.</summary>
    public int Status { get; }

    /// <summary>Gets the headers, matched without regard to case.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Gets the body as UTF-8 text.</summary>
    public string Body { get; }

    /// <summary>
    /// Reads everything the server sends until it closes the connection, then parses it. The read has a real-time safety limit,
    /// so a server that never closes fails the test instead of hanging it. A connection the server reset before it sent any
    /// byte reads as no answer, like one it closed, because a close with unread request bytes may reset the connection.
    /// </summary>
    /// <param name="stream">The connection.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The answer.</returns>
    public static async Task<HookBridgeTestResponse> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(SafetyLimit);
        try
        {
            await stream.CopyToAsync(buffer, limit.Token).ConfigureAwait(true);
        }
        catch (IOException) when (buffer.Length == 0)
        {
            // Reset without an answer.
        }

        return Parse(buffer.ToArray());
    }

    /// <summary>Parses the raw bytes of an answer.</summary>
    /// <param name="raw">The bytes the server sent.</param>
    /// <returns>The answer.</returns>
    public static HookBridgeTestResponse Parse(byte[] raw)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (raw.Length == 0)
        {
            return new HookBridgeTestResponse(0, headers, string.Empty);
        }

        var end = raw.AsSpan().IndexOf("\r\n\r\n"u8);
        var lines = Encoding.ASCII.GetString(raw, 0, end).Split("\r\n");
        var status = int.Parse(lines[0].Split(' ')[1], CultureInfo.InvariantCulture);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            headers[line[..colon]] = line[(colon + 1)..].Trim();
        }

        return new HookBridgeTestResponse(status, headers, Encoding.UTF8.GetString(raw, end + 4, raw.Length - end - 4));
    }

    /// <summary>Parses the body as JSON.</summary>
    /// <returns>The root element, which stays valid after the call.</returns>
    public JsonElement Json()
    {
        using var document = JsonDocument.Parse(Body);
        return document.RootElement.Clone();
    }

    /// <summary>Returns the names of the properties of the JSON body.</summary>
    /// <returns>The names, in the order the server wrote them.</returns>
    public IReadOnlyList<string> PropertyNames() => [.. Json().EnumerateObject().Select(static property => property.Name)];
}
