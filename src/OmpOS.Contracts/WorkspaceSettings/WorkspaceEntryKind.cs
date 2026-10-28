using System.Text.Json.Serialization;

namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>The kind of an entry of a workspace folder listing.</summary>
public enum WorkspaceEntryKind
{
    /// <summary>A file.</summary>
    [JsonStringEnumMemberName("file")]
    File,

    /// <summary>A folder.</summary>
    [JsonStringEnumMemberName("directory")]
    Directory,
}
