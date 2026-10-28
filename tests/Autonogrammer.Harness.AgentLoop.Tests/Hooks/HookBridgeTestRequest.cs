using System.Globalization;
using System.Text;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>Builds the raw bytes of requests to the bridge, valid ones and deliberately broken ones.</summary>
internal static class HookBridgeTestRequest
{
    /// <summary>Builds a valid hook request: <c>POST /v1/hook</c> with a JSON body and a matching <c>Content-Length</c>.</summary>
    /// <param name="json">The body.</param>
    /// <param name="authorization">The value of the <c>Authorization</c> header, or null to send none.</param>
    /// <param name="extraHeaders">Further header lines, without the line break.</param>
    /// <returns>The request bytes.</returns>
    public static byte[] Post(string json, string? authorization, params string[] extraHeaders)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var head = new StringBuilder("POST /v1/hook HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Type: application/json\r\n");
        if (authorization is not null)
        {
            head.Append("Authorization: ").Append(authorization).Append("\r\n");
        }

        head.Append("Content-Length: ").Append(body.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        foreach (var header in extraHeaders)
        {
            head.Append(header).Append("\r\n");
        }

        head.Append("\r\n");
        return [.. Encoding.ASCII.GetBytes(head.ToString()), .. body];
    }

    /// <summary>Builds a request from a head written out in full.</summary>
    /// <param name="head">The request line and the header lines, each ending in a line break. The blank line is added.</param>
    /// <param name="body">The body.</param>
    /// <returns>The request bytes.</returns>
    public static byte[] Raw(string head, string body = "") => [.. Encoding.Latin1.GetBytes(head + "\r\n"), .. Encoding.UTF8.GetBytes(body)];

    /// <summary>Builds a JSON hook body of exactly the given size in bytes, by padding the payload.</summary>
    /// <param name="size">The size in bytes. It must be at least the size of the unpadded body.</param>
    /// <returns>The JSON text.</returns>
    public static string JsonOfSize(int size)
    {
        const string prefix = "{\"event\":\"stop\",\"payload\":{\"pad\":\"";
        const string suffix = "\"}}";
        return prefix + new string('x', size - prefix.Length - suffix.Length) + suffix;
    }
}
