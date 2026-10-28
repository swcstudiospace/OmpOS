using System.Text.Json.Serialization;

namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>
/// Source-generated JSON metadata for the workspace settings shapes of contract 1.4.0: conventions, runtime, plugins, network, hooks, automations and
/// the read-only workspace tree and file. It is a feature context of its own: it adds shapes without growing the core contract context.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ConventionsResponse))]
[JsonSerializable(typeof(RuntimeSettingsResponse))]
[JsonSerializable(typeof(PluginsSettingsResponse))]
[JsonSerializable(typeof(NetworkSettingsResponse))]
[JsonSerializable(typeof(HooksResponse))]
[JsonSerializable(typeof(AutomationsResponse))]
[JsonSerializable(typeof(WorkspaceTreeResponse))]
[JsonSerializable(typeof(WorkspaceFileResponse))]
[JsonSerializable(typeof(ConventionCommandInfo))]
[JsonSerializable(typeof(ConventionSkillInfo))]
[JsonSerializable(typeof(ConventionRuleInfo))]
[JsonSerializable(typeof(ConventionSubagentInfo))]
[JsonSerializable(typeof(ConventionConfigFileInfo))]
[JsonSerializable(typeof(ConventionDiagnosticInfo))]
[JsonSerializable(typeof(WorkspacePluginInfo))]
[JsonSerializable(typeof(NetworkEndpointInfo))]
[JsonSerializable(typeof(WorkspaceHookInfo))]
[JsonSerializable(typeof(WorkspaceHookSourceInfo))]
[JsonSerializable(typeof(WorkspaceHookDiagnosticInfo))]
[JsonSerializable(typeof(WorkspaceAutomationInfo))]
[JsonSerializable(typeof(AutomationLastRunInfo))]
[JsonSerializable(typeof(AutomationPauseInfo))]
[JsonSerializable(typeof(WorkspaceTreeEntry))]
[JsonSerializable(typeof(ConventionScopeKind))]
[JsonSerializable(typeof(ConventionDiagnosticCode))]
[JsonSerializable(typeof(ConventionItemType))]
[JsonSerializable(typeof(ConventionRuleType))]
[JsonSerializable(typeof(WorkspaceEntryKind))]
public sealed partial class WorkspaceSettingsJsonContext : JsonSerializerContext
{
}
