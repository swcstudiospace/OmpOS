using System.Text.Json.Serialization;
using OmpOS.Contracts.Errors;
using OmpOS.Contracts.Intents;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Contracts.Workflows;

namespace OmpOS.Contracts.Serialization;

/// <summary>
/// Source-generated JSON metadata for every contract shape. Consumers pass
/// <c>Default</c> type info instead of relying on reflection, which stays
/// disabled for trimming (WASM) and AOT compatibility.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Session[]))]
[JsonSerializable(typeof(Session))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(SessionStatus))]
[JsonSerializable(typeof(IntentEnvelope))]
[JsonSerializable(typeof(IntentKind))]
[JsonSerializable(typeof(Diagnostic))]
[JsonSerializable(typeof(UiBlock))]
[JsonSerializable(typeof(MarkdownBlock))]
[JsonSerializable(typeof(CodeBlock))]
[JsonSerializable(typeof(ToolCallBlock))]
[JsonSerializable(typeof(PermissionBlock))]
[JsonSerializable(typeof(PlanBlock))]
[JsonSerializable(typeof(DiffPlaceholderBlock))]
[JsonSerializable(typeof(ErrorBlock))]
[JsonSerializable(typeof(A2uiSurfaceBlock))]
[JsonSerializable(typeof(UnknownBlock))]
[JsonSerializable(typeof(UiBlock[]))]
[JsonSerializable(typeof(StreamEvent))]
[JsonSerializable(typeof(StreamEvent[]))]
[JsonSerializable(typeof(StreamEventPayload))]
[JsonSerializable(typeof(TextDeltaPayload))]
[JsonSerializable(typeof(UiBlockPayload))]
[JsonSerializable(typeof(ErrorPayload))]
[JsonSerializable(typeof(DiagnosticPayload))]
[JsonSerializable(typeof(CompletedPayload))]
[JsonSerializable(typeof(UnknownPayload))]
[JsonSerializable(typeof(SandboxDescriptor))]
[JsonSerializable(typeof(Isolation))]
[JsonSerializable(typeof(Attestation))]
[JsonSerializable(typeof(SandboxStatus))]
[JsonSerializable(typeof(WorkflowRun))]
[JsonSerializable(typeof(WorkflowRunStatus))]
[JsonSerializable(typeof(OmpOSError))]
[JsonSerializable(typeof(ErrorCode))]
public sealed partial class OmpOsJsonContext : JsonSerializerContext
{
}
