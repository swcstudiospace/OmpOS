namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// The only choke point before injection. Evaluation order is <see cref="DenialCodeCatalog.GateStepOrder"/> (ADR-0035 D3).
/// An exception inside <see cref="AuthorizeAsync"/> is <see cref="DenialCode.InternalError"/>. Time comes from an injected
/// <see cref="TimeProvider"/>; this contract does not read the clock.
/// </summary>
public interface IComputerActionGate
{
    /// <summary>Authorises one host-built request.</summary>
    /// <param name="request">The host-built request.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>Allowed with a 2 second single-use ticket, or denied with a code and message id.</returns>
    ValueTask<ComputerGateDecision> AuthorizeAsync(ComputerActionRequest request, CancellationToken cancellationToken);
}

