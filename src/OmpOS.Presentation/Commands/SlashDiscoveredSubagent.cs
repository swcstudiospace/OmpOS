namespace OmpOS.Presentation.Commands;

/// <summary>A subagent definition that discovery found in an <c>agents</c> folder.</summary>
/// <param name="Name">The subagent name, which is the name typed after the slash.</param>
/// <param name="Description">What the subagent is for; empty when the file gives none.</param>
/// <param name="Model">The model the subagent asks for (<c>inherit</c> or a model id), or <see langword="null"/>.</param>
/// <param name="ReadOnly">Whether the subagent must not change files.</param>
/// <param name="IsBackground">Whether the subagent runs in the background.</param>
/// <param name="Instructions">The subagent's instructions: the body of its file.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="Origin">The file to show: relative to the workspace for a project file, <c>~/</c> relative for a user file.</param>
public sealed record SlashDiscoveredSubagent(
    string Name,
    string Description,
    string? Model,
    bool ReadOnly,
    bool IsBackground,
    string Instructions,
    SlashScope Scope,
    string Origin);
