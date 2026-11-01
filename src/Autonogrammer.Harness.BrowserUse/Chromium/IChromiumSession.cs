namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>One host-owned Chromium process. Disposal is the host's job after CDP close and a process-tree kill.</summary>
public interface IChromiumSession : IAsyncDisposable
{
    /// <summary>Gets the process id.</summary>
    int ProcessId { get; }

    /// <summary>Gets the profile directory the host created.</summary>
    string ProfileDirectory { get; }

    /// <summary>Gets the observed working set in bytes.</summary>
    long WorkingSetBytes { get; }

    /// <summary>Gets the open tab count.</summary>
    int TabCount { get; }

    /// <summary>Opens one tab.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the tab is open.</returns>
    Task OpenTabAsync(CancellationToken cancellationToken);

    /// <summary>Closes the browser through the DevTools protocol.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the close was attempted.</returns>
    Task CloseViaCdpAsync(CancellationToken cancellationToken);

    /// <summary>Kills the process tree. Called after <see cref="CloseViaCdpAsync"/>.</summary>
    void KillProcessTree();

    /// <summary>Reads <c>Browser.getVersion</c>.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version string.</returns>
    Task<string> GetVersionAsync(CancellationToken cancellationToken);

    /// <summary>Starts a screencast and waits for one frame.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when one frame arrived.</returns>
    Task CaptureOneFrameAsync(CancellationToken cancellationToken);
}
