using System.Text.Json.Serialization;

namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>Where a discovered convention item lives.</summary>
public enum ConventionScopeKind
{
    /// <summary>A directory of the workspace folder.</summary>
    [JsonStringEnumMemberName("project")]
    Project,

    /// <summary>The home directory of the API process. Present only when <c>Api:Workspace:IncludeUserScope</c> is true.</summary>
    [JsonStringEnumMemberName("user")]
    User,
}
