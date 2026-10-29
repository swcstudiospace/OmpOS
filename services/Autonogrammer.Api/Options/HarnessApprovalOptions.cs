using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Api.Options;

/// <summary>
/// The <c>Harness:Acp:Approval</c> section: which tool permission requests of the engine may reach the user-approval flow of a direct run,
/// and which of them a user decision may allow. Nothing is approved automatically: an admitted request is registered as a pending approval,
/// waits for the owner's decision in the UI, and is denied when no decision arrives within <see cref="DecisionTimeoutSeconds"/>.
/// </summary>
/// <remarks>
/// <para>
/// Defaults are deny everything: with no <see cref="Kinds"/> every permission request is denied at once, and with no
/// <see cref="GrantableTools"/> an <c>allow</c> decision is refused (HTTP 403).
/// </para>
/// <para>
/// Safe recipe for a single-owner deployment: <c>Kinds</c> = <c>read, search, edit, delete, move, execute</c> and <c>GrantableTools</c> = <c>*</c>.
/// Every such action then waits for the owner's decision in the UI and is denied on timeout. Kinds that are not listed (<c>fetch</c>, <c>think</c>,
/// <c>switch_mode</c>, <c>other</c>) stay denied at once. Tool names in <see cref="GrantableTools"/> are matched exactly (ordinal) against the tool
/// name the engine sends with the request, the one the UI shows in the pending approval.
/// </para>
/// </remarks>
internal sealed class HarnessApprovalOptions
{
    /// <summary>Gets the wildcard entry of <see cref="GrantableTools"/>: a decision may allow any tool whose kind <see cref="Kinds"/> admits.</summary>
    public const string AnyTool = "*";

    /// <summary>Gets the longest decision timeout, in seconds, that is accepted.</summary>
    public const int MaxDecisionTimeoutSeconds = 3600;

    /// <summary>
    /// Gets the ACP tool kinds the engine can send with a permission request, as the Agent Client Protocol names them.
    /// </summary>
    public static IReadOnlyList<string> AcpToolKinds { get; } = ["read", "edit", "delete", "move", "search", "execute", "think", "fetch", "switch_mode", "other"];

    /// <summary>
    /// Gets or sets the ACP tool kinds whose permission requests may reach the user-approval flow. Every other kind is denied at once, and
    /// the run records a diagnostic. Each entry must be one of <see cref="AcpToolKinds"/>. Empty by default, which denies every request.
    /// </summary>
    public List<string> Kinds { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the tools a user decision may allow, or <see cref="AnyTool"/> to permit any tool whose kind is admitted.
    /// Empty by default: an <c>allow</c> decision is refused and only a <c>deny</c> is accepted.
    /// </summary>
    public List<string> GrantableTools { get; set; } = [];

    /// <summary>
    /// Gets or sets how long an admitted request waits for the user's decision, in seconds, before it is denied. Defaults to the shared
    /// permission decision window (30 seconds); the accepted range is 1 to <see cref="MaxDecisionTimeoutSeconds"/>.
    /// </summary>
    public int DecisionTimeoutSeconds { get; set; } = (int)PermissionDecisionWindow.Default.TotalSeconds;

    /// <summary>Gets the decision timeout as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan DecisionTimeout => TimeSpan.FromSeconds(DecisionTimeoutSeconds);

    /// <summary>Builds the engine-side approval policy: deny everything when no kind is configured, otherwise admit exactly <see cref="Kinds"/>.</summary>
    /// <returns>The policy.</returns>
    public AcpApprovalPolicy CreatePolicy() => Kinds.Count == 0 ? AcpApprovalPolicy.DenyAll : new AcpApprovalPolicy(Kinds);
}
