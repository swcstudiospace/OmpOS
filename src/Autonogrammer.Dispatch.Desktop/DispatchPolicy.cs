namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// The desktop's own dispatch policy. A controller can never raise it. Every capability is off until the user enables it here.
/// Session read and control are always available to a paired controller, subject to the operation rules.
/// </summary>
public sealed record DispatchPolicy
{
    /// <summary>Gets whether dispatched code prompts run. Default off.</summary>
    public bool CodeEnabled { get; init; }

    /// <summary>Gets whether dispatched cowork tasks run. Default off.</summary>
    public bool CoworkEnabled { get; init; }

    /// <summary>Gets whether a dispatched cowork task may name Browser Use. Default off.</summary>
    public bool BrowserUseEnabled { get; init; }

    /// <summary>Gets the on-desktop opt-in that lets a dispatched cowork task name Computer Use. Default off.</summary>
    public bool ComputerUseOptIn { get; init; }

    /// <summary>Gets whether the user paused Dispatch from an approval prompt. A paused desktop starts no new work from a controller.</summary>
    public bool Paused { get; init; }
}
