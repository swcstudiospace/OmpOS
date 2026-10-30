namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>How serious a <see cref="SafetyDiagnostic"/> is.</summary>
public enum SafetyDiagnosticSeverity
{
    /// <summary>Part of a file was ignored; the rest applies.</summary>
    Warning = 0,

    /// <summary>The whole file was ignored.</summary>
    Error = 1,
}
