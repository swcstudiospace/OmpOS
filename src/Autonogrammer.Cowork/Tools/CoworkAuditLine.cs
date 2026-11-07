namespace Autonogrammer.Cowork.Tools;

/// <summary>One JSONL audit line. It holds no arguments and no result bytes.</summary>
/// <param name="Seq">The sequence number in the file.</param>
/// <param name="Time">The UTC time.</param>
/// <param name="SessionId">The session id.</param>
/// <param name="Tool">The tool name.</param>
/// <param name="Origin">The turn origin.</param>
/// <param name="Policy">The gate outcome.</param>
/// <param name="Approval">The broker outcome, or null.</param>
/// <param name="IsError">Whether the model reads an error.</param>
/// <param name="ResultKinds">The result kinds.</param>
/// <param name="Duration">The duration in milliseconds.</param>
/// <param name="Target">The host-observed target, or null.</param>
/// <param name="Prev">The SHA-256 of the previous line.</param>
public sealed record CoworkAuditLine(
    long Seq,
    DateTimeOffset Time,
    string SessionId,
    string Tool,
    string Origin,
    string Policy,
    string? Approval,
    bool IsError,
    string ResultKinds,
    double Duration,
    string? Target,
    string Prev);
