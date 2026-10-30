using System.Text.Json;

namespace Autonogrammer.Harness.AgentLoop.Tests.Live;

/// <summary>
/// One JSON-RPC response to a request of the live handshake test.
/// </summary>
/// <param name="Result">The <c>result</c> member, or null when the response is an error.</param>
/// <param name="ErrorCode">The JSON-RPC error code, or null when the request succeeded.</param>
/// <param name="ErrorMessage">The error message, or null when the request succeeded.</param>
internal sealed record LiveAcpResponse(JsonElement? Result, long? ErrorCode, string? ErrorMessage)
{
    /// <summary>Gets a value indicating whether the response is an error.</summary>
    public bool IsError => ErrorCode is not null;

    /// <summary>Describes the response without the result body, for assertion messages.</summary>
    /// <returns>The description.</returns>
    public string Describe() => IsError ? $"error {ErrorCode}: {ErrorMessage}" : "ok";
}
