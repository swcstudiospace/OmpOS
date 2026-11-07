using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Window, availability and permission reads. The gate does not take the injection backend.</summary>
public interface IComputerObservation
{
    /// <summary>Reports whether Computer Use can arm.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The availability state.</returns>
    ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken);

    /// <summary>Reports permission facts.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>One status per relevant permission.</returns>
    ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken);

    /// <summary>Lists windows bottom to top.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>Window facts.</returns>
    ValueTask<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken);
}
