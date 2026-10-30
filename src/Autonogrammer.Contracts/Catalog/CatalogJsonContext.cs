using System.Text.Json.Serialization;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Contracts.Ui;

namespace Autonogrammer.Contracts.Catalog;

/// <summary>
/// Source-generated JSON metadata for the session catalog, board, workspace, source-offer, stage and context shapes. It is a feature context of
/// its own: it adds shapes without growing the core contract context. Stream payload and block types are listed because
/// <see cref="SessionCatalogEntry"/> carries the session log and their converters resolve derived types through the same context.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SessionCatalogEntry[]))]
[JsonSerializable(typeof(SessionCatalogEntry))]
[JsonSerializable(typeof(BoardResponse))]
[JsonSerializable(typeof(BoardColumn))]
[JsonSerializable(typeof(BoardCard))]
[JsonSerializable(typeof(BoardStateKind))]
[JsonSerializable(typeof(CardDetailsResponse))]
[JsonSerializable(typeof(WorkspaceInfo))]
[JsonSerializable(typeof(PluginCheckResult))]
[JsonSerializable(typeof(PluginCheckStatus))]
[JsonSerializable(typeof(SourceInfo))]
[JsonSerializable(typeof(SessionStagesResponse))]
[JsonSerializable(typeof(SessionContextResponse))]
[JsonSerializable(typeof(SessionLifecycleResponse))]
[JsonSerializable(typeof(CatalogChangedEvent))]
[JsonSerializable(typeof(SessionStatusEvent))]
[JsonSerializable(typeof(CatalogResyncEvent))]
[JsonSerializable(typeof(CatalogSessionKind))]
[JsonSerializable(typeof(CatalogRowGroup))]
[JsonSerializable(typeof(CatalogRowStatus))]
[JsonSerializable(typeof(CatalogChangeKind))]
[JsonSerializable(typeof(StageEvent))]
[JsonSerializable(typeof(StageSource))]
[JsonSerializable(typeof(StageStatus))]
[JsonSerializable(typeof(TaskContext))]
[JsonSerializable(typeof(ContextNote))]
[JsonSerializable(typeof(GsdPhaseInfo))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(SessionStatus))]
[JsonSerializable(typeof(StreamEvent))]
[JsonSerializable(typeof(StreamEvent[]))]
[JsonSerializable(typeof(StreamEventPayload))]
[JsonSerializable(typeof(TextDeltaPayload))]
[JsonSerializable(typeof(UiBlockPayload))]
[JsonSerializable(typeof(ErrorPayload))]
[JsonSerializable(typeof(DiagnosticPayload))]
[JsonSerializable(typeof(UserMessagePayload))]
[JsonSerializable(typeof(CompletedPayload))]
[JsonSerializable(typeof(UnknownPayload))]
[JsonSerializable(typeof(UiBlock))]
[JsonSerializable(typeof(MarkdownBlock))]
[JsonSerializable(typeof(CodeBlock))]
[JsonSerializable(typeof(ToolCallBlock))]
[JsonSerializable(typeof(PermissionBlock))]
[JsonSerializable(typeof(PlanBlock))]
[JsonSerializable(typeof(DiffPlaceholderBlock))]
[JsonSerializable(typeof(ToolResultBlock))]
[JsonSerializable(typeof(FileEditBlock))]
[JsonSerializable(typeof(ErrorBlock))]
[JsonSerializable(typeof(A2uiSurfaceBlock))]
[JsonSerializable(typeof(UnknownBlock))]
[JsonSerializable(typeof(UserMessageBlock))]
public sealed partial class CatalogJsonContext : JsonSerializerContext
{
}
