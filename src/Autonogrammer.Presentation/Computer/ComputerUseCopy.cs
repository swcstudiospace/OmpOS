namespace Autonogrammer.Presentation.Computer;

/// <summary>Every text of the Computer Use approval, indicator, advisor and settings surfaces.</summary>
public static class ComputerUseCopy
{
    /// <summary>Navigation label of the Computer Use page.</summary>
    public const string Title = "Computer";

    /// <summary>Heading of the Computer Use page.</summary>
    public const string Heading = "Computer use";

    /// <summary>Sentence under the heading.</summary>
    public const string Description =
        "Computer Use can see and act on this desktop only after you allow it here. Approvals stay on this computer.";

    /// <summary>Provider disclosure heading.</summary>
    public const string ProviderDisclosureHeading = "Provider disclosure";

    /// <summary>What the model provider receives. No window title and no typed text.</summary>
    public const string ProviderDisclosure =
        "Screenshots and host-built action facts can be sent to the model provider for this session. Typed text and clipboard contents are not sent as text. You can turn Computer Use off here.";

    /// <summary>Opt-in heading.</summary>
    public const string OptInHeading = "Opt in";

    /// <summary>Opt-in body.</summary>
    public const string OptInBody =
        "Computer Use is off until you opt in. Allowing it does not approve any action. Each action still asks on this desktop.";

    /// <summary>Accept opt-in.</summary>
    public const string OptInAccept = "Opt in";

    /// <summary>Decline opt-in.</summary>
    public const string OptInDecline = "Keep off";

    /// <summary>Grants heading.</summary>
    public const string GrantsHeading = "Grants";

    /// <summary>Empty grants.</summary>
    public const string GrantsEmpty = "No grants.";

    /// <summary>Revoke a grant.</summary>
    public const string Revoke = "Revoke";

    /// <summary>Denylist heading.</summary>
    public const string DenylistHeading = "Denylist additions";

    /// <summary>Denylist note. Built-in rows cannot be removed.</summary>
    public const string DenylistNote = "Built-in rows are read-only. You can add an application identity. You cannot remove a built-in row here.";

    /// <summary>Add denylist entry.</summary>
    public const string DenylistAdd = "Add";

    /// <summary>Kill chord heading.</summary>
    public const string KillChordHeading = "Kill chord";

    /// <summary>Kill chord description.</summary>
    public const string KillChordDescription = "The chord that stops Computer Use. Leave it empty to keep the default.";

    /// <summary>Security phrase heading.</summary>
    public const string SecurityPhraseHeading = "Security phrase";

    /// <summary>Security phrase description.</summary>
    public const string SecurityPhraseDescription =
        "Shown on the approval dialog and the indicator so you can tell them from a model-drawn window. Own-process windows are masked.";

    /// <summary>Audit heading.</summary>
    public const string AuditHeading = "Audit";

    /// <summary>Audit note. Thumbnails off. Typed text is length only.</summary>
    public const string AuditNote = "The last 30 days. Thumbnails are off. Typed text is recorded as a length, never as the text.";

    /// <summary>Empty audit.</summary>
    public const string AuditEmpty = "No audit rows.";

    /// <summary>Indicator test heading.</summary>
    public const string IndicatorTestHeading = "Indicator test";

    /// <summary>Indicator test button.</summary>
    public const string IndicatorTest = "Test indicator";

    /// <summary>Approval window title.</summary>
    public const string ApprovalTitle = "Allow Computer Use?";

    /// <summary>Deny.</summary>
    public const string Deny = "Deny";

    /// <summary>Allow once.</summary>
    public const string AllowOnce = "Allow once";

    /// <summary>Allow for this session.</summary>
    public const string AllowForSession = "Allow for this session";

    /// <summary>Label on the host-sanitised preview. Not a model-text field.</summary>
    public const string AgentTextLabel = "agent text";

    /// <summary>Indicator headline.</summary>
    public const string IndicatorHeadline = "Autonogrammer is controlling this computer";

    /// <summary>Armed and not acting.</summary>
    public const string ArmedIdle = "armed, idle";

    /// <summary>A ticket is in flight.</summary>
    public const string Acting = "acting";

    /// <summary>Stop.</summary>
    public const string Stop = "Stop";

    /// <summary>Local origin.</summary>
    public const string LocalOrigin = "Local";

    /// <summary>Open System Settings.</summary>
    public const string OpenSystemSettings = "Open System Settings";

    /// <summary>Formats a grant row. No window title.</summary>
    /// <param name="sessionId">Session id.</param>
    /// <param name="appIdentity">Application identity.</param>
    /// <param name="tier">Tier name.</param>
    /// <param name="origin">Origin name.</param>
    /// <param name="expiry">Expiry text.</param>
    /// <returns>The row text.</returns>
    public static string GrantRow(string sessionId, string appIdentity, string tier, string origin, string expiry) =>
        sessionId + " · " + appIdentity + " · " + tier + " · " + origin + " · " + expiry;

    /// <summary>Formats the character count and the first sanitised characters of agent text.</summary>
    /// <param name="count">Character count.</param>
    /// <param name="preview">At most 40 sanitised characters.</param>
    /// <returns>The preview line.</returns>
    public static string AgentTextLine(int count, string preview) => count + " characters. " + preview;
}
