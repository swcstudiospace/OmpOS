using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Native.Composition.ComputerUse;

/// <summary>L1 status port over the OS backend. It does not decide policy.</summary>
internal sealed class ComputerStatusAdapter : IComputerStatusSource
{
    private readonly IComputerUseBackend _backend;

    /// <summary>Creates the adapter.</summary>
    /// <param name="backend">The backend the head registered.</param>
    /// <exception cref="ArgumentNullException"><paramref name="backend"/> is null.</exception>
    public ComputerStatusAdapter(IComputerUseBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        _backend = backend;
    }

    /// <inheritdoc />
    public ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken) =>
        _backend.GetAvailabilityAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken) =>
        _backend.GetPermissionStatusAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<ComputerPermissionStatus> RequestPermissionAsync(ComputerPermissionKind kind, CancellationToken cancellationToken) =>
        _backend.RequestPermissionAsync(kind, cancellationToken);
}
