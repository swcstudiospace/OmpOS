using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Rpc;

/// <summary>The response to one command, correlated by <see cref="Id"/>.</summary>
public sealed record RpcResponse
{
    /// <summary>Gets the frame discriminator; always <c>response</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the id of the command this response answers.</summary>
    [JsonConverter(typeof(RpcIdConverter))]
    public string? Id { get; init; }

    /// <summary>Gets the command type this response answers.</summary>
    public string? Command { get; init; }

    /// <summary>Gets whether omp executed the command.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the command result when <see cref="Success"/> is true.</summary>
    public JsonElement? Data { get; init; }

    /// <summary>Gets the failure text when <see cref="Success"/> is false.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the machine-readable failure code, when omp provides one.</summary>
    public string? Code { get; init; }
}
