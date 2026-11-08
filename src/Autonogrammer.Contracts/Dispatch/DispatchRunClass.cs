namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Run-limit classes (ADR-0036). Session and command-cancel operations have the short session limit; every other operation has the task limit.
/// </summary>
public static class DispatchRunClass
{
    /// <summary>Gets the operations that have the session run limit.</summary>
    public static IReadOnlyList<DispatchOp> SessionScopedOps { get; } =
    [
        DispatchOp.SessionList,
        DispatchOp.SessionSteer,
        DispatchOp.SessionCancel,
        DispatchOp.CommandCancel,
    ];

    /// <summary>Reports whether an operation has the session run limit.</summary>
    /// <param name="op">The operation.</param>
    /// <returns><see langword="true"/> for the session and command-cancel operations.</returns>
    public static bool IsSessionScoped(DispatchOp op) => SessionScopedOps.Contains(op);
}
