namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>Why a request to the Substrate server failed. The reason never carries response text or a credential.</summary>
public enum SubstrateFailureReason
{
    /// <summary>The server could not be reached or the connection broke.</summary>
    Transport,

    /// <summary>The request did not finish within its time limit.</summary>
    Timeout,

    /// <summary>The server answered with a redirect. Redirects are never followed.</summary>
    Redirected,

    /// <summary>The response came from an origin other than the configured endpoint.</summary>
    OtherOrigin,

    /// <summary>The server answered with a status outside 200-299.</summary>
    HttpStatus,

    /// <summary>The response body is larger than the 1 MiB cap.</summary>
    ResponseTooLarge,

    /// <summary>The response is not the shape the contract describes.</summary>
    InvalidResponse,

    /// <summary>The MCP server answered a tool call with a JSON-RPC error.</summary>
    ProtocolError,

    /// <summary>The configured bearer token cannot be sent as an HTTP header value.</summary>
    InvalidCredential,
}
