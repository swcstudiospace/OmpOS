namespace OmpOS.Contracts.Environments;

/// <summary>How serious a <see cref="RemoteDiagnosis"/> is.</summary>
public enum DiagnosisSeverity
{
    /// <summary>The environment works but something is unverified or unusual.</summary>
    Warning,

    /// <summary>The environment cannot run a session until the fix is applied.</summary>
    Error,
}
