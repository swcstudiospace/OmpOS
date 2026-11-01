namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Wire names for <see cref="DispatchOp"/>. Unknown text, including every <c>computer.*</c> value, fails.</summary>
public static class DispatchOpWire
{
    /// <summary>Parses one closed operation name.</summary>
    /// <param name="text">The wire value.</param>
    /// <param name="op">The operation when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is one of the seven closed names.</returns>
    public static bool TryParse(string? text, out DispatchOp op)
    {
        switch (text)
        {
            case "session.list":
                op = DispatchOp.SessionList;
                return true;
            case "session.steer":
                op = DispatchOp.SessionSteer;
                return true;
            case "session.cancel":
                op = DispatchOp.SessionCancel;
                return true;
            case "approval.decide":
                op = DispatchOp.ApprovalDecide;
                return true;
            case "code.prompt":
                op = DispatchOp.CodePrompt;
                return true;
            case "cowork.task":
                op = DispatchOp.CoworkTask;
                return true;
            case "command.cancel":
                op = DispatchOp.CommandCancel;
                return true;
            default:
                op = default;
                return false;
        }
    }

    /// <summary>Returns the wire name of a closed operation.</summary>
    /// <param name="op">The operation.</param>
    /// <returns>The exact protocol string.</returns>
    public static string ToWire(DispatchOp op) => op switch
    {
        DispatchOp.SessionList => "session.list",
        DispatchOp.SessionSteer => "session.steer",
        DispatchOp.SessionCancel => "session.cancel",
        DispatchOp.ApprovalDecide => "approval.decide",
        DispatchOp.CodePrompt => "code.prompt",
        DispatchOp.CoworkTask => "cowork.task",
        DispatchOp.CommandCancel => "command.cancel",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };
}
