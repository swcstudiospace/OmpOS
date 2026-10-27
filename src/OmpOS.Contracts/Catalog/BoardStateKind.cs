using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Catalog;

/// <summary>The state of the whole board. The server answers <see cref="Empty"/> or <see cref="Ready"/>; <see cref="Loading"/> and <see cref="Error"/> exist so a client models all four states of the desktop board with one type.</summary>
public enum BoardStateKind
{
    /// <summary>The sessions are being read.</summary>
    [JsonStringEnumMemberName("loading")]
    Loading,

    /// <summary>No session exists.</summary>
    [JsonStringEnumMemberName("empty")]
    Empty,

    /// <summary>Reading the sessions failed.</summary>
    [JsonStringEnumMemberName("error")]
    Error,

    /// <summary>At least one session exists.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,
}
