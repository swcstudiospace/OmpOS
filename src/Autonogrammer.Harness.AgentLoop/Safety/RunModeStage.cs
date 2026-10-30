namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>The step of the decision engine that settled a call, for the audit record.</summary>
public enum RunModeStage
{
    /// <summary>No rule and no check applies: the call asks.</summary>
    Default = 0,

    /// <summary>A deny rule matched.</summary>
    DenyRule = 1,

    /// <summary>An allow rule matched.</summary>
    AllowRule = 2,

    /// <summary>The mode itself decided: Run Everything allows every call.</summary>
    Mode = 3,

    /// <summary>A read-only command from the built-in list of known-safe commands (Auto-review only).</summary>
    KnownSafe = 4,

    /// <summary>The read boundary decided a read.</summary>
    ReadBoundary = 5,

    /// <summary>A write inside the workspace was allowed, or a write outside it asks.</summary>
    WriteScope = 6,

    /// <summary>The call touches a protected path (Autonogrammer and editor settings, git hooks): it always asks.</summary>
    ProtectedPath = 7,

    /// <summary>The call is missing what the engine needs to judge it (no command, no path), or is of an unknown kind: it asks.</summary>
    UnknownRequest = 8,

    /// <summary>The existing approval policy forbids the call, so no run mode can allow it.</summary>
    PolicyCeiling = 9,

    /// <summary>Cowork Auto settled the call from an allow rule, a deny rule, or the known-safe list.</summary>
    CoworkAutoRule = 10,

    /// <summary>Cowork asked because the call deletes, in every permission mode.</summary>
    CoworkDeleteProtection = 11,

    /// <summary>A tainted Cowork session was treated as Manual.</summary>
    CoworkTaintDemotion = 12,

    /// <summary>Skip was not available, so the call was treated as Manual.</summary>
    CoworkSkipGate = 13,

    /// <summary>The Cowork folder boundary refused the path.</summary>
    CoworkFolderBoundary = 14,
}
