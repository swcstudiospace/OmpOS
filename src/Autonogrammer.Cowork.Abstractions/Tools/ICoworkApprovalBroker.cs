namespace Autonogrammer.Cowork.Abstractions;

/// <summary>Asks a person, and is the only broker a ByProviderGate provider may use.</summary>
public interface ICoworkApprovalBroker
{
    /// <summary>Waits for a decision.</summary>
    /// <param name="request">The ask.</param>
    /// <param name="cancellationToken">Cancels the wait. The host cancels it at the approval timeout.</param>
    /// <returns>The decision.</returns>
    ValueTask<CoworkApprovalDecision> RequestAsync(CoworkApprovalRequest request, CancellationToken cancellationToken);
}
