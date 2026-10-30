namespace Autonogrammer.Harness.Secrets;

/// <summary>The outputs that must never carry a secret.</summary>
public enum SecretSinkChannel
{
    /// <summary>Application log lines.</summary>
    Log,

    /// <summary>Recorded agent and terminal transcripts.</summary>
    Transcript,

    /// <summary>Crash and diagnostic reports.</summary>
    CrashReport,

    /// <summary>Outbound telemetry.</summary>
    Telemetry,
}
