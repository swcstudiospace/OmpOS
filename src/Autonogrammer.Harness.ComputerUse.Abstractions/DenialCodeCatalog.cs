namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Message ids and the ADR-0035 D3 / 73-CONTEXT D-06 evaluation order.</summary>
public static class DenialCodeCatalog
{
    /// <summary>
    /// Gate evaluation order, first failing step wins (73-CONTEXT D-06):
    /// (1) enabled, signed in, opted in (Disabled, NotOptedIn), kill-switch trip or revocation (Revoked);
    /// (2) platform and permissions (PlatformUnavailable, PermissionMissing), kill switch registered and indicator visible (KillSwitchUnavailable, IndicatorUnavailable);
    /// (3) frame valid, frame_id is the session's latest and layout unchanged (StaleFrame), coordinates inside (OutOfBounds);
    /// (4) host-observed target from ListWindows at decision time, never from the model;
    /// (5) denylist and own windows (DeniedApplication, OwnWindow), grant lookup (NoGrant), tier (TierTooLow), forbidden chords (ForbiddenChord), ElevatedTarget, SecureDesktop;
    /// (6) rate and step limit (RateLimited, StepLimit), user takeover (UserTookOver), image budget (ImageBudget);
    /// (7) sensitivity classification through IComputerApprovalPrompt (ApprovalDenied, ApprovalTimeout, NoLocalApprover);
    /// (8) write-ahead audit record (AuditUnavailable).
    /// Exceptions map to InternalError. FrameTooLarge is a capture outcome, not a gate step.
    /// </summary>
    public const string GateStepOrder =
        "(1) enabled, signed in, opted in (Disabled, NotOptedIn), kill-switch trip or revocation (Revoked); " +
        "(2) platform and permissions (PlatformUnavailable, PermissionMissing), kill switch registered and indicator visible (KillSwitchUnavailable, IndicatorUnavailable); " +
        "(3) frame valid, frame_id is the session's latest and layout unchanged (StaleFrame), coordinates inside (OutOfBounds); " +
        "(4) host-observed target from ListWindows at decision time, never from the model; " +
        "(5) denylist and own windows (DeniedApplication, OwnWindow), grant lookup (NoGrant), tier (TierTooLow), forbidden chords (ForbiddenChord), ElevatedTarget, SecureDesktop; " +
        "(6) rate and step limit (RateLimited, StepLimit), user takeover (UserTookOver), image budget (ImageBudget); " +
        "(7) sensitivity classification through IComputerApprovalPrompt (ApprovalDenied, ApprovalTimeout, NoLocalApprover); " +
        "(8) write-ahead audit record (AuditUnavailable).";

    /// <summary>Returns the single message id for <paramref name="code"/>.</summary>
    /// <param name="code">The denial code.</param>
    /// <returns>The message id.</returns>
    public static string UserMessageId(DenialCode code) => code switch
    {
        DenialCode.Disabled => "computer.denial.disabled",
        DenialCode.NotOptedIn => "computer.denial.not-opted-in",
        DenialCode.Revoked => "computer.denial.revoked",
        DenialCode.PlatformUnavailable => "computer.denial.platform-unavailable",
        DenialCode.PermissionMissing => "computer.denial.permission-missing",
        DenialCode.KillSwitchUnavailable => "computer.denial.kill-switch-unavailable",
        DenialCode.IndicatorUnavailable => "computer.denial.indicator-unavailable",
        DenialCode.StaleFrame => "computer.denial.stale-frame",
        DenialCode.OutOfBounds => "computer.denial.out-of-bounds",
        DenialCode.DeniedApplication => "computer.denial.denied-application",
        DenialCode.OwnWindow => "computer.denial.own-window",
        DenialCode.NoGrant => "computer.denial.no-grant",
        DenialCode.TierTooLow => "computer.denial.tier-too-low",
        DenialCode.ForbiddenChord => "computer.denial.forbidden-chord",
        DenialCode.ElevatedTarget => "computer.denial.elevated-target",
        DenialCode.SecureDesktop => "computer.denial.secure-desktop",
        DenialCode.RateLimited => "computer.denial.rate-limited",
        DenialCode.StepLimit => "computer.denial.step-limit",
        DenialCode.UserTookOver => "computer.denial.user-took-over",
        DenialCode.ImageBudget => "computer.denial.image-budget",
        DenialCode.FrameTooLarge => "computer.denial.frame-too-large",
        DenialCode.ApprovalDenied => "computer.denial.approval-denied",
        DenialCode.ApprovalTimeout => "computer.denial.approval-timeout",
        DenialCode.NoLocalApprover => "computer.denial.no-local-approver",
        DenialCode.AuditUnavailable => "computer.denial.audit-unavailable",
        DenialCode.InternalError => "computer.denial.internal-error",
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };
}
