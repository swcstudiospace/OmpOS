namespace OmpOS.Api.Cloud;

/// <summary>The label that ties a worker to its cloud agent. A worker is removed only when this label names an agent, or, for an orphan, is present and names no live agent.</summary>
internal static class CloudWorkerLabels
{
    /// <summary>The label key: <c>io.ompos.cloud-agent</c>. Its value is the agent identifier.</summary>
    public const string AgentKey = "io.ompos.cloud-agent";
}
