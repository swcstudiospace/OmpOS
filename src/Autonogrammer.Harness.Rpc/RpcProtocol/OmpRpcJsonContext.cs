using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Rpc;

/// <summary>
/// Source-generated JSON metadata for every omp RPC command and frame. Reflection-based serialization is disabled
/// repository-wide, so every type that crosses the wire must be listed here.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(NegotiateProtocolCommand))]
[JsonSerializable(typeof(GetStateCommand))]
[JsonSerializable(typeof(PromptCommand))]
[JsonSerializable(typeof(AbortCommand))]
[JsonSerializable(typeof(AbortAndRestoreQueueCommand))]
[JsonSerializable(typeof(SwitchSessionCommand))]
[JsonSerializable(typeof(UnknownCommandProbe))]
[JsonSerializable(typeof(RpcImageContent))]
[JsonSerializable(typeof(ReadyFrame))]
[JsonSerializable(typeof(RpcResponse))]
[JsonSerializable(typeof(RpcChunkFrame))]
[JsonSerializable(typeof(PromptResultFrame))]
[JsonSerializable(typeof(RpcPromptError))]
[JsonSerializable(typeof(RpcFrameErrorFrame))]
internal sealed partial class OmpRpcJsonContext : JsonSerializerContext
{
    /// <summary>Serializes one command as a single JSON object with no trailing newline.</summary>
    /// <param name="command">The command to serialize.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentException">The command type is not registered in this context.</exception>
    public static string SerializeCommand(RpcCommand command) => command switch
    {
        NegotiateProtocolCommand c => JsonSerializer.Serialize(c, Default.NegotiateProtocolCommand),
        GetStateCommand c => JsonSerializer.Serialize(c, Default.GetStateCommand),
        PromptCommand c => JsonSerializer.Serialize(c, Default.PromptCommand),
        AbortCommand c => JsonSerializer.Serialize(c, Default.AbortCommand),
        AbortAndRestoreQueueCommand c => JsonSerializer.Serialize(c, Default.AbortAndRestoreQueueCommand),
        SwitchSessionCommand c => JsonSerializer.Serialize(c, Default.SwitchSessionCommand),
        UnknownCommandProbe c => JsonSerializer.Serialize(c, Default.UnknownCommandProbe),
        _ => throw new ArgumentException("The RPC command type is not registered for serialization.", nameof(command)),
    };
}
