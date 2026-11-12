using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Native.Cowork.ComputerUse;

/// <summary>
/// The local Computer Use approval prompt. It asks through <see cref="CoworkApprovalBroker"/>, the same card queue
/// the conversation reads, and every request is local-only. Only an explicit allow is returned as an allow; every other
/// outcome, including timeout, unavailability and refusal, is <see cref="ComputerApprovalChoice.Deny"/>.
/// </summary>
internal sealed class ProductionApprovalPrompt : IComputerApprovalPrompt
{
    /// <summary>The tool class named on the approval card.</summary>
    public const string ToolName = "computer_use";

    private readonly CoworkApprovalBroker _broker;

    /// <summary>Creates the prompt.</summary>
    /// <param name="broker">The broker the conversation queue reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="broker"/> is null.</exception>
    /// <exception cref="ArgumentException">The broker waits longer than <see cref="ComputerApprovalFacts.Timeout"/>.</exception>
    public ProductionApprovalPrompt(CoworkApprovalBroker broker)
    {
        ArgumentNullException.ThrowIfNull(broker);
        if (broker.ApprovalTimeout > ComputerApprovalFacts.Timeout)
        {
            throw new ArgumentException("The approval broker must time out no later than the Computer Use dialog.", nameof(broker));
        }

        _broker = broker;
    }

    /// <inheritdoc/>
    public async ValueTask<ComputerApprovalChoice> RequestAsync(ComputerApprovalFacts facts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(facts);
        cancellationToken.ThrowIfCancellationRequested();
        CoworkApprovalRequest request = Build(facts);
        CoworkApprovalDecision decision = await _broker.RequestAsync(request, cancellationToken).ConfigureAwait(false);
        return Map(decision.Outcome);
    }

    private static CoworkApprovalRequest Build(ComputerApprovalFacts facts)
    {
        // The facts carry no session id, and the broker never reads it. The call id is generated, so no model text names it.
        CoworkCallPreview preview = new(
            facts.HostActionWords,
            $"{facts.ApplicationName} - {facts.SanitizedTitle}",
            Details(facts),
            Grant: null);
        return new CoworkApprovalRequest(
            SessionId: string.Empty,
            CallId: Guid.NewGuid().ToString("N"),
            ToolName: ToolName,
            Preview: preview,
            Origin: facts.Origin == ComputerActionOrigin.Dispatch ? CoworkTurnOrigin.Dispatch : CoworkTurnOrigin.Local,
            MustBeLocal: true);
    }

    private static List<string> Details(ComputerApprovalFacts facts)
    {
        List<string> details = [facts.AppIdentity, facts.HostSanitizedPreview];
        if (!string.IsNullOrEmpty(facts.DeviceName))
        {
            details.Add(facts.DeviceName);
        }

        return details;
    }

    private static ComputerApprovalChoice Map(CoworkApprovalOutcome outcome) => outcome switch
    {
        CoworkApprovalOutcome.Allowed => ComputerApprovalChoice.AllowOnce,
        CoworkApprovalOutcome.AllowedAlways => ComputerApprovalChoice.AllowForSession,
        _ => ComputerApprovalChoice.Deny,
    };
}
