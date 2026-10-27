using System.Buffers;
using System.Globalization;
using System.Text;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// The status codes, error codes and wire format of the bridge's answers. Every answer is an HTTP/1.1 response that closes the
/// connection. An error body is the small JSON object <c>{"error":"&lt;code&gt;"}</c> and never echoes anything the client sent.
/// </summary>
internal static class HookBridgeHttpResponse
{
    /// <summary>The status of a decision. It is also what the head reader reports for a head it could parse.</summary>
    internal const int Ok = 200;

    /// <summary>The status of a request that is not valid HTTP or not a valid hook request.</summary>
    internal const int BadRequest = 400;

    /// <summary>The status of a request with a missing or wrong bearer token. The body is empty.</summary>
    internal const int Unauthorized = 401;

    /// <summary>The status of a request from a peer that is not on the loopback interface.</summary>
    internal const int Forbidden = 403;

    /// <summary>The status of a request for a path other than the hook path.</summary>
    internal const int NotFound = 404;

    /// <summary>The status of a request with a method other than POST.</summary>
    internal const int MethodNotAllowed = 405;

    /// <summary>The status of a request without a <c>Content-Length</c>, or with a <c>Transfer-Encoding</c>.</summary>
    internal const int LengthRequired = 411;

    /// <summary>The status of a request that announces a body larger than the cap.</summary>
    internal const int ContentTooLarge = 413;

    /// <summary>The status of a request whose head is larger than the cap.</summary>
    internal const int HeadersTooLarge = 431;

    /// <summary>The status of a request that arrives while the server stops or is stopped.</summary>
    internal const int Unavailable = 503;

    /// <summary>The value that stands for "no answer was sent", for a client that vanished or was too slow. It is not an HTTP status.</summary>
    internal const int NoResponse = 0;

    /// <summary>The error code of a request that is not valid HTTP.</summary>
    internal const string BadRequestCode = "bad_request";

    /// <summary>The error code of a body that is not a valid hook request in JSON.</summary>
    internal const string InvalidJsonCode = "invalid_json";

    /// <summary>The error code of a request whose <c>event</c> is missing or blank.</summary>
    internal const string MissingEventCode = "missing_event";

    /// <summary>The error code of a request whose <c>payload</c> is present but not a JSON object.</summary>
    internal const string InvalidPayloadCode = "invalid_payload";

    /// <summary>The error code of a request from a peer that is not on the loopback interface.</summary>
    internal const string ForbiddenCode = "forbidden";

    /// <summary>The error code of a request for the wrong path.</summary>
    internal const string NotFoundCode = "not_found";

    /// <summary>The error code of a request with the wrong method.</summary>
    internal const string MethodNotAllowedCode = "method_not_allowed";

    /// <summary>The error code of a request that does not announce its body length.</summary>
    internal const string LengthRequiredCode = "length_required";

    /// <summary>The error code of a body over the cap.</summary>
    internal const string ContentTooLargeCode = "payload_too_large";

    /// <summary>The error code of a head over the cap.</summary>
    internal const string HeadersTooLargeCode = "headers_too_large";

    /// <summary>The error code of a request that arrives when the session is closed or the server is stopping.</summary>
    internal const string SessionClosedCode = "session_closed";

    private const int CoalesceLimitBytes = 16 * 1024;

    /// <summary>Builds the body of an error answer.</summary>
    /// <param name="code">One of the error codes. It must not need JSON escaping.</param>
    /// <returns>The UTF-8 JSON object.</returns>
    internal static byte[] ErrorBody(string code) => Encoding.UTF8.GetBytes("{\"error\":\"" + code + "\"}");

    /// <summary>
    /// Writes one complete answer: the status line, the headers, then the body. The answer always carries
    /// <c>Connection: close</c> and <c>Cache-Control: no-store</c>; a 401 names the <c>Bearer</c> scheme and a 405 the allowed
    /// method.
    /// </summary>
    /// <param name="stream">The connection.</param>
    /// <param name="status">The HTTP status.</param>
    /// <param name="body">The body, empty for none. It is sent as <c>application/json</c>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the answer has been written.</returns>
    internal static async ValueTask WriteAsync(Stream stream, int status, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var head = new StringBuilder(192);
        head.Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {status} {ReasonPhrase(status)}\r\n");
        if (body.Length > 0)
        {
            head.Append("Content-Type: application/json\r\n");
        }

        head.Append(CultureInfo.InvariantCulture, $"Content-Length: {body.Length}\r\n");
        if (status == Unauthorized)
        {
            head.Append("WWW-Authenticate: Bearer\r\n");
        }
        else if (status == MethodNotAllowed)
        {
            head.Append("Allow: POST\r\n");
        }

        head.Append("Cache-Control: no-store\r\nConnection: close\r\n\r\n");
        var headBytes = Encoding.ASCII.GetBytes(head.ToString());
        if (body.Length > CoalesceLimitBytes)
        {
            await stream.WriteAsync(headBytes, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            return;
        }

        // A small answer goes out in one segment, so a client that reads headers and body separately never waits on a second one.
        var total = headBytes.Length + body.Length;
        var rented = ArrayPool<byte>.Shared.Rent(total);
        try
        {
            headBytes.AsSpan().CopyTo(rented);
            body.Span.CopyTo(rented.AsSpan(headBytes.Length));
            await stream.WriteAsync(rented.AsMemory(0, total), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    private static string ReasonPhrase(int status) => status switch
    {
        Ok => "OK",
        BadRequest => "Bad Request",
        Unauthorized => "Unauthorized",
        Forbidden => "Forbidden",
        NotFound => "Not Found",
        MethodNotAllowed => "Method Not Allowed",
        LengthRequired => "Length Required",
        ContentTooLarge => "Content Too Large",
        HeadersTooLarge => "Request Header Fields Too Large",
        Unavailable => "Service Unavailable",
        _ => "Error",
    };
}
