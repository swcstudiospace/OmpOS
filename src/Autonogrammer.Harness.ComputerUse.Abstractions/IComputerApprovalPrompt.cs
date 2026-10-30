namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>The only UI port for Computer Use approval. Implementations deny when <see cref="ComputerApprovalFacts.Timeout"/> elapses.</summary>
public interface IComputerApprovalPrompt
{
    /// <summary>Shows host-built facts and waits for a local choice.</summary>
    /// <param name="facts">Host-observed facts. No model-text field.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The choice. Timeout is <see cref="ComputerApprovalChoice.Deny"/>.</returns>
    ValueTask<ComputerApprovalChoice> RequestAsync(ComputerApprovalFacts facts, CancellationToken cancellationToken);
}
