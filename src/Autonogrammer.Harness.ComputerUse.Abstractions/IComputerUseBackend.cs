namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// One operating-system adapter. Every I/O member returns <see cref="ValueTask"/>, ends in <c>Async</c>, and takes
/// <see cref="CancellationToken"/> last with no default. Coordinates are native virtual-desktop pixels.
/// This type names no OS backend.
/// </summary>
public interface IComputerUseBackend
{
    /// <summary>The platform this adapter speaks.</summary>
    ComputerPlatform Platform { get; }

    /// <summary>Optional capabilities. Focused-element inspection is off in v0.16.0.</summary>
    ComputerBackendCapabilities Capabilities { get; }

    /// <summary>Reports whether Computer Use can arm on this host.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The availability state.</returns>
    ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken);

    /// <summary>Reports permission facts. Does not prompt.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>One status per relevant permission.</returns>
    ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken);

    /// <summary>Raises the operating-system permission prompt for an explicit user action.</summary>
    /// <param name="kind">The permission to request.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The status after the prompt returns.</returns>
    ValueTask<ComputerPermissionStatus> RequestPermissionAsync(ComputerPermissionKind kind, CancellationToken cancellationToken);

    /// <summary>Lists displays.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>Display facts.</returns>
    ValueTask<IReadOnlyList<DisplayInfo>> ListDisplaysAsync(CancellationToken cancellationToken);

    /// <summary>Lists windows bottom-to-top in z-order (index 0 is bottom-most).</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>Window facts.</returns>
    ValueTask<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken);

    /// <summary>Reads the pointer in native pixels.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The pointer state.</returns>
    ValueTask<PointerState> GetPointerAsync(CancellationToken cancellationToken);

    /// <summary>Captures native BGRA pixels for one display or sub-rectangle.</summary>
    /// <param name="region">The native region.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The frame.</returns>
    ValueTask<RawFrame> CaptureAsync(CaptureRegion region, CancellationToken cancellationToken);

    /// <summary>Injects one primitive. Policy is not decided here.</summary>
    /// <param name="primitive">The primitive.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>A completed task.</returns>
    ValueTask InjectAsync(InputPrimitive primitive, CancellationToken cancellationToken);

    /// <summary>Lifts every held button and key.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>A completed task.</returns>
    ValueTask ReleaseAllInputAsync(CancellationToken cancellationToken);

    /// <summary>Reads clipboard text. Prefer the head-provided <see cref="IClipboardTextPort"/> when the head supplies one.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The text, or <see langword="null"/> when the clipboard has none.</returns>
    ValueTask<string?> ReadClipboardTextAsync(CancellationToken cancellationToken);

    /// <summary>Writes clipboard text.</summary>
    /// <param name="text">The text to write.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>A completed task.</returns>
    ValueTask WriteClipboardTextAsync(string text, CancellationToken cancellationToken);
}
