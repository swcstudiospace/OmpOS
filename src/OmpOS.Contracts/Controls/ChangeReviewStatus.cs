using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Controls;

/// <summary>The review state of a change.</summary>
public enum ChangeReviewStatus
{
    /// <summary>No decision was recorded for the change as it stands.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,

    /// <summary>The user accepted the change.</summary>
    [JsonStringEnumMemberName("accepted")]
    Accepted,

    /// <summary>The user rejected the change.</summary>
    [JsonStringEnumMemberName("rejected")]
    Rejected,
}
