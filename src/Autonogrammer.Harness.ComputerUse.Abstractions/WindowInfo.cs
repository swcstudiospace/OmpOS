namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// One window. <see cref="IComputerUseBackend.ListWindowsAsync"/> returns these bottom-to-top in z-order
/// (index 0 is the bottom-most window).
/// </summary>
public sealed record WindowInfo
{
    /// <summary>Stable window id for this session.</summary>
    public required string Id { get; init; }

    /// <summary>Owning process id.</summary>
    public required int Pid { get; init; }

    /// <summary>Platform application identity (bundle id, executable plus signer, or WM_CLASS plus exe basename).</summary>
    public required string AppIdentity { get; init; }

    /// <summary>Window title as observed by the host. The model does not supply this.</summary>
    public required string Title { get; init; }

    /// <summary>Native bounds.</summary>
    public required PixelRect Bounds { get; init; }

    /// <summary><see langword="true"/> when the window is minimised.</summary>
    public required bool Minimized { get; init; }

    /// <summary><see langword="true"/> when the window is focused.</summary>
    public required bool Focused { get; init; }

    /// <summary><see langword="true"/> when the window belongs to this Autonogrammer process.</summary>
    public required bool IsOwnProcess { get; init; }
}

