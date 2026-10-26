using System.Text.Json.Serialization;

namespace OmpOS.Harness.Acp.AcpProtocol;

/// <summary>
/// Source-generated serializer context for every ACP frame and payload this client reads or writes. Reflection-based
/// serialization is disabled project-wide, so every type that crosses the wire must be listed here.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(AcpFrames.Inbound))]
[JsonSerializable(typeof(AcpFrames.Request<AcpFrames.InitializeParams>))]
[JsonSerializable(typeof(AcpFrames.Request<AcpFrames.AuthenticateParams>))]
[JsonSerializable(typeof(AcpFrames.Request<AcpFrames.NewSessionParams>))]
[JsonSerializable(typeof(AcpFrames.Request<AcpFrames.LoadSessionParams>))]
[JsonSerializable(typeof(AcpFrames.Request<AcpFrames.CloseSessionParams>))]
[JsonSerializable(typeof(AcpFrames.Request<AcpFrames.PromptParams>))]
[JsonSerializable(typeof(AcpFrames.Notification<AcpFrames.CancelParams>))]
[JsonSerializable(typeof(AcpFrames.Notification<AcpFrames.CancelRequestParams>))]
[JsonSerializable(typeof(AcpFrames.Response<AcpFrames.PermissionResponse>))]
[JsonSerializable(typeof(AcpFrames.Response<AcpFrames.FsReadResult>))]
[JsonSerializable(typeof(AcpFrames.Response<AcpFrames.FsWriteResult>))]
[JsonSerializable(typeof(AcpFrames.FsReadParams))]
[JsonSerializable(typeof(AcpFrames.FsWriteParams))]
[JsonSerializable(typeof(AcpFrames.ErrorResponse))]
[JsonSerializable(typeof(AcpFrames.StringErrorResponse))]
[JsonSerializable(typeof(AcpFrames.InitializeResult))]
[JsonSerializable(typeof(AcpFrames.NewSessionResult))]
[JsonSerializable(typeof(AcpFrames.PromptResult))]
[JsonSerializable(typeof(AcpFrames.SessionNotification))]
[JsonSerializable(typeof(AcpFrames.SessionUpdateHeader))]
[JsonSerializable(typeof(AcpFrames.ChunkUpdate))]
[JsonSerializable(typeof(AcpFrames.ToolCallPayload))]
[JsonSerializable(typeof(AcpFrames.PlanUpdate))]
[JsonSerializable(typeof(AcpFrames.McpServerFrame))]
[JsonSerializable(typeof(AcpFrames.NameValueFrame))]
[JsonSerializable(typeof(AcpFrames.PermissionRequestParams))]
public sealed partial class AcpJsonContext : JsonSerializerContext
{
}
