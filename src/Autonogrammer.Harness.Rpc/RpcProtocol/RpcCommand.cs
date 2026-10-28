using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Rpc;

/// <summary>Base of every command written to omp's stdin. Serialized through <see cref="OmpRpcJsonContext"/>.</summary>
public abstract record RpcCommand
{
    /// <summary>Gets the wire command type, for example <c>get_state</c>.</summary>
    public abstract string Type { get; }

    /// <summary>Gets the correlation id that omp echoes on the matching response.</summary>
    [JsonConverter(typeof(RpcIdConverter))]
    public string? Id { get; init; }
}
