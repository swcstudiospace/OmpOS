using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Controls;

/// <summary>Whether a change created a file or edited one.</summary>
public enum ChangeKind
{
    /// <summary>The edit created the file.</summary>
    [JsonStringEnumMemberName("added")]
    Added,

    /// <summary>The edit changed an existing file.</summary>
    [JsonStringEnumMemberName("modified")]
    Modified,
}
