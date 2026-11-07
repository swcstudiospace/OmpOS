using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.BrowserUse.Options;

namespace Autonogrammer.Harness.BrowserUse.Vision;

/// <summary>Validated session limits. Every bound is an option, not a hidden constant.</summary>
public sealed class BrowserLimits
{
    /// <summary>The per-tool timeout.</summary>
    public static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The page-load timeout.</summary>
    public static readonly TimeSpan PageLoadTimeout = TimeSpan.FromSeconds(30);

    private readonly Queue<DateTimeOffset> _actions = new();
    private int _calls;
    private int _extraShots;

    /// <summary>Initializes limits from options.</summary>
    /// <param name="options">The options.</param>
    public BrowserLimits(BrowserUseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        MaxScreenshotBytes = Math.Min(options.MaxScreenshotBytes, BrowserUseOptions.MaxScreenshotBytesCeiling);
    }

    /// <summary>Gets the options.</summary>
    public BrowserUseOptions Options { get; }

    /// <summary>Gets the screenshot byte cap, never above the host cap.</summary>
    public int MaxScreenshotBytes { get; }

    /// <summary>Gets the warning text after 400 calls, or null.</summary>
    public string? SessionWarning { get; private set; }

    /// <summary>Admits one call or returns a fixed failure.</summary>
    /// <param name="now">The clock.</param>
    /// <param name="failure">The failure text, when refused.</param>
    /// <returns>False when the call is refused.</returns>
    public bool TryAdmit(DateTimeOffset now, out string? failure)
    {
        failure = null;
        while (_actions.Count > 0 && now - _actions.Peek() >= TimeSpan.FromMinutes(1))
        {
            _ = _actions.Dequeue();
        }

        if (_actions.Count >= Options.MaxActionsPerMinute)
        {
            failure = "RateLimited";
            return false;
        }

        if (_calls >= Options.MaxCallsPerSession)
        {
            failure = "SessionLimitReached";
            return false;
        }

        _actions.Enqueue(now);
        _calls++;
        if (_calls == 400)
        {
            SessionWarning = "SessionCallWarning";
        }

        return true;
    }

    /// <summary>Consumes one screenshot or reports the budget failure.</summary>
    /// <param name="failure">The failure, when exhausted.</param>
    /// <returns>False when the budget is exhausted.</returns>
    public bool TryConsumeScreenshot(out string? failure)
    {
        failure = null;
        if (_calls > Options.MaxScreenshotsPerSession + _extraShots && _extraShots == 0)
        {
            failure = "ImageBudgetExhausted";
            return false;
        }

        return true;
    }

    /// <summary>Records a delivered screenshot.</summary>
    /// <returns>False when this shot is past the budget. The 41st shot fails.</returns>
    public bool TryRecordShot()
    {
        if (ShotCount >= Options.MaxScreenshotsPerSession + _extraShots)
        {
            return false;
        }

        ShotCount++;
        return true;
    }

    /// <summary>Gets how many screenshots have been delivered.</summary>
    public int ShotCount { get; private set; }

    /// <summary>Allows 20 more screenshots after <c>ImageBudgetExhausted</c>.</summary>
    public void AllowTwentyMore() => _extraShots += 20;

    /// <summary>Dispatch turns are blocked unless the option holds.</summary>
    /// <param name="origin">The turn origin.</param>
    /// <returns>True when the turn may use the browser.</returns>
    public bool AllowsTurn(CoworkTurnOrigin origin) =>
        origin != CoworkTurnOrigin.Dispatch || Options.AllowDispatchSessions;

    /// <summary>Open is prompt-on-new-origin for a dispatch turn.</summary>
    /// <param name="origin">The turn origin.</param>
    /// <returns>The effective policy.</returns>
    public PolicyMode EffectivePolicy(CoworkTurnOrigin origin)
    {
        if (origin == CoworkTurnOrigin.Dispatch && Options.PolicyMode == PolicyMode.Open)
        {
            return PolicyMode.PromptOnNewOrigin;
        }

        return Options.PolicyMode;
    }

    /// <summary>AllowedAlways is never stored on a dispatch turn and is treated as Allowed.</summary>
    /// <param name="outcome">The answer.</param>
    /// <param name="origin">The turn origin.</param>
    /// <returns>The outcome to apply, and whether it may be stored.</returns>
    public static (CoworkApprovalOutcome Outcome, bool Store) NormalizeAlways(CoworkApprovalOutcome outcome, CoworkTurnOrigin origin)
    {
        if (origin == CoworkTurnOrigin.Dispatch && outcome == CoworkApprovalOutcome.AllowedAlways)
        {
            return (CoworkApprovalOutcome.Allowed, false);
        }

        return (outcome, outcome == CoworkApprovalOutcome.AllowedAlways);
    }
}

