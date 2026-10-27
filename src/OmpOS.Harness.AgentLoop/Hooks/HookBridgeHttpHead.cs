namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// The parts of a request head the bridge acts on, as <see cref="HookBridgeHttpParser"/> reads them. Every other header is
/// validated and then dropped.
/// </summary>
internal sealed class HookBridgeHttpHead
{
    /// <summary>Gets the request method exactly as sent.</summary>
    public required string Method { get; init; }

    /// <summary>Gets the request target exactly as sent. It always starts with <c>/</c>.</summary>
    public required string Target { get; init; }

    /// <summary>Gets the value of the one <c>Authorization</c> header, or null when there is none.</summary>
    public string? Authorization { get; init; }

    /// <summary>Gets the value of the one <c>Content-Length</c> header, or null when there is none. A value too large for a long reads as <see cref="long.MaxValue"/>.</summary>
    public long? ContentLength { get; init; }

    /// <summary>Gets a value saying that the head has a <c>Transfer-Encoding</c> header. The bridge does not read chunked or otherwise encoded bodies.</summary>
    public bool HasTransferEncoding { get; init; }
}
