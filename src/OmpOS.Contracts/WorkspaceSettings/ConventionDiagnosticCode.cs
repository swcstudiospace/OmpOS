using System.Text.Json.Serialization;

namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>Why a convention file produced no item or lost to another item.</summary>
public enum ConventionDiagnosticCode
{
    /// <summary>The file is not valid UTF-8 text or its frontmatter is malformed.</summary>
    [JsonStringEnumMemberName("malformed")]
    Malformed,

    /// <summary>The file is larger than the size cap.</summary>
    [JsonStringEnumMemberName("too-large")]
    TooLarge,

    /// <summary>The file or folder could not be read.</summary>
    [JsonStringEnumMemberName("unreadable")]
    Unreadable,

    /// <summary>A symbolic link resolves outside the allowed folder, so the path was refused.</summary>
    [JsonStringEnumMemberName("escapes-root")]
    EscapesRoot,

    /// <summary>Another item with the same name won by precedence.</summary>
    [JsonStringEnumMemberName("duplicate")]
    Duplicate,

    /// <summary>The file is well formed but cannot be used.</summary>
    [JsonStringEnumMemberName("invalid")]
    Invalid,
}
