using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Providers;

/// <summary>The outcome of a provider test. A test never sends a key, so it says only whether something answers at the address.</summary>
public enum ProviderTestStatus
{
    /// <summary>No call was made. The reason says why.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,

    /// <summary>Something answered at the address.</summary>
    [JsonStringEnumMemberName("reachable")]
    Reachable,

    /// <summary>Something answered and asked for a key, which is what a keyed endpoint does.</summary>
    [JsonStringEnumMemberName("unauthorized")]
    Unauthorized,

    /// <summary>Nothing answered, or the answer was a server error.</summary>
    [JsonStringEnumMemberName("unreachable")]
    Unreachable,

    /// <summary>The service refused to call the address because it is on the service's own machine or network.</summary>
    [JsonStringEnumMemberName("blocked")]
    Blocked,
}
