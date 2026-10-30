namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Receives every decision the <see cref="RunModePermissionResolver"/> makes. An implementation must not throw and must not block.</summary>
public interface IRunModeAuditSink
{
    /// <summary>Records one decision.</summary>
    /// <param name="entry">The decision.</param>
    void Record(RunModeAuditEntry entry);
}
