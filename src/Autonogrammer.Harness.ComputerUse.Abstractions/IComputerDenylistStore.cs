namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>Denylist. Built-in rows are read-only. User additions are add-only through <see cref="AddAsync"/>.</summary>
public interface IComputerDenylistStore
{
    /// <summary>Built-in rows. Callers cannot remove them.</summary>
    IReadOnlyList<ComputerDenylistEntry> BuiltInRows { get; }

    /// <summary>Adds a user denylist entry. This member does not remove built-in rows.</summary>
    /// <param name="appIdentity">The application identity to deny.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>A completed task.</returns>
    ValueTask AddAsync(string appIdentity, CancellationToken cancellationToken);
}

