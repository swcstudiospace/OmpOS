using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Catalog;

/// <summary>What a catalog row is. The wire value is the lower-case member name.</summary>
public enum CatalogSessionKind
{
    /// <summary>A chat session: the engine runs behind the agent panel.</summary>
    [JsonStringEnumMemberName("agent")]
    Agent,

    /// <summary>An omp terminal session.</summary>
    [JsonStringEnumMemberName("terminal")]
    Terminal,

    /// <summary>A cloud or remote-environment session.</summary>
    [JsonStringEnumMemberName("cloud")]
    Cloud,
}
