namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>Loads and saves <see cref="ComputerUseUserSettings"/>.</summary>
public interface IComputerUseSettingsStore
{
    /// <summary>Loads settings. Missing files return defaults, not an error.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The settings.</returns>
    ValueTask<ComputerUseUserSettings> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Saves settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>A completed task.</returns>
    ValueTask SaveAsync(ComputerUseUserSettings settings, CancellationToken cancellationToken);
}
