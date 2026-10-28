using System.ComponentModel.DataAnnotations;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>
/// Binds the workspace-reading keys of <c>Api:Workspace</c> (the folder itself, <c>Api:Workspace:Root</c>, is read through <see cref="WorkspaceLocator"/>).
/// The settings and workspace endpoints are read-only: they never write a file and never start a process.
/// </summary>
internal sealed class WorkspaceSettingsOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Api:Workspace";

    /// <summary>Gets the default number of settings and workspace reads allowed per minute.</summary>
    public const int DefaultRequestsPerMinute = 120;

    /// <summary>
    /// Gets or sets a value indicating whether the user directories (<c>~/.autonogrammer</c>, <c>~/.cursor</c> and the others) of the API process home are read
    /// next to the workspace folder. The default is false: a hosted API serves many people and its process home belongs to none of them.
    /// </summary>
    public bool IncludeUserScope { get; set; }

    /// <summary>
    /// Gets or sets the subjects that may read the workspace and settings endpoints. Empty means every authenticated caller. A caller outside a
    /// non-empty list gets 404, as a caller of another owner's session does.
    /// </summary>
    public List<string> Owners { get; set; } = [];

    /// <summary>Gets or sets the settings and workspace reads allowed per minute across all callers. Defaults to 120.</summary>
    [Range(1, 100_000, ErrorMessage = "Api:Workspace:RequestsPerMinute must be between 1 and 100000.")]
    public int RequestsPerMinute { get; set; } = DefaultRequestsPerMinute;
}
