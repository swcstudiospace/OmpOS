namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>
/// Status slice the permission advisor polls. Read-only except <see cref="RequestPermissionAsync"/>, which raises the
/// operating-system prompt only for an explicit user action. The head's status adapter implements this over the backend.
/// </summary>
public interface IComputerStatusSource
{
    /// <summary>Current availability.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The availability state.</returns>
    ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken);

    /// <summary>Current permission facts.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>Permission statuses.</returns>
    ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken);

    /// <summary>Raises the operating-system prompt for an explicit user action.</summary>
    /// <param name="kind">The permission to request.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The status after the prompt returns.</returns>
    ValueTask<ComputerPermissionStatus> RequestPermissionAsync(ComputerPermissionKind kind, CancellationToken cancellationToken);
}

