using System.Globalization;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Contracts.Cowork;

/// <summary>The Cowork task cap and the sentence a full list or a full registry shows.</summary>
public static class CoworkSessionLimit
{
    /// <summary>The most Cowork tasks the registry holds.</summary>
    public const int MaxSessions = AgentSessionLimits.MaxSessions;

    /// <summary>The sentence a full Cowork list or a full Cowork registry shows.</summary>
    public static string FullMessage { get; } = "You have " + MaxSessions.ToString(CultureInfo.InvariantCulture) + " Cowork tasks. Delete or archive one to start another.";
}
