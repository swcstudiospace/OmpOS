namespace OmpOS.Api.Services;

/// <summary>
/// Binds <c>Api:Workspace</c>: the folder the API applies change decisions in. Unset means the API has no workspace folder, and an accept or reject
/// is recorded without writing a file.
/// </summary>
internal sealed class SessionWorkspaceOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Api:Workspace";

    /// <summary>Gets or sets the absolute path of the workspace folder. Empty means not configured.</summary>
    public string Root { get; set; } = string.Empty;
}
