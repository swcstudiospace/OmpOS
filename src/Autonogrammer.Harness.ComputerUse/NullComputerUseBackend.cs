using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse;

/// <summary>
/// Backend a head registers when the platform or the head has none.
/// Availability is <see cref="ComputerAvailability.UnsupportedPlatform"/> with message id <c>computer.unavailable.platform</c>.
/// </summary>
public sealed class NullComputerUseBackend : IComputerUseBackend
{
    /// <inheritdoc/>
    public ComputerPlatform Platform => ComputerPlatform.Unsupported;

    /// <inheritdoc/>
    public ComputerBackendCapabilities Capabilities => ComputerBackendCapabilities.None;

    /// <inheritdoc/>
    public ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(ComputerAvailability.UnsupportedPlatform);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken) =>
        throw Unavailable();

    /// <inheritdoc/>
    public ValueTask<ComputerPermissionStatus> RequestPermissionAsync(ComputerPermissionKind kind, CancellationToken cancellationToken) =>
        throw Unavailable();

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DisplayInfo>> ListDisplaysAsync(CancellationToken cancellationToken) =>
        throw Unavailable();

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken) =>
        throw Unavailable();

    /// <inheritdoc/>
    public ValueTask<PointerState> GetPointerAsync(CancellationToken cancellationToken) =>
        throw Unavailable();

    /// <inheritdoc/>
    public ValueTask<RawFrame> CaptureAsync(CaptureRegion region, CancellationToken cancellationToken) =>
        throw Unavailable();

    /// <inheritdoc/>
    public ValueTask InjectAsync(InputPrimitive primitive, CancellationToken cancellationToken) =>
        throw Unavailable();

    /// <inheritdoc/>
    public ValueTask ReleaseAllInputAsync(CancellationToken cancellationToken) =>
        throw Unavailable();

    /// <inheritdoc/>
    public ValueTask<string?> ReadClipboardTextAsync(CancellationToken cancellationToken) =>
        throw Unavailable();

    /// <inheritdoc/>
    public ValueTask WriteClipboardTextAsync(string text, CancellationToken cancellationToken) =>
        throw Unavailable();

    private static ComputerBackendException Unavailable() => new(ComputerFailure.Unavailable);
}
