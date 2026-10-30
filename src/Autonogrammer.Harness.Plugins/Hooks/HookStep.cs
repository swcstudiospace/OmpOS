namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The outcome of one hook inside a dispatch, before the outcomes are merged.
/// </summary>
/// <param name="Record">The log record of the hook.</param>
/// <param name="Output">The hook's parsed answer, or null when it gave none or it is not used.</param>
/// <param name="DenyCause">Why the hook denies on its own account (consent, quarantine, exit code, failure policy), or none.</param>
/// <param name="DenyAgentMessage">The message the model sees for a synthetic deny.</param>
/// <param name="DenyUserMessage">The message the user sees for a synthetic deny.</param>
/// <param name="Ran">True when a process or model call was started.</param>
internal sealed record HookStep(
    HookRunRecord Record,
    HookOutputParse? Output,
    HookDecisionCause DenyCause,
    string? DenyAgentMessage,
    string? DenyUserMessage,
    bool Ran);
