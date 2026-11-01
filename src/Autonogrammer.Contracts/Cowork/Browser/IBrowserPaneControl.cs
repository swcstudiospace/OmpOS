namespace Autonogrammer.Contracts.Cowork.Browser;

/// <summary>
/// Desktop commands for the pane. The pane view calls <see cref="ReleaseControlAsync"/> on F6.
/// Remote heads have no implementation that sends input.
/// </summary>
public interface IBrowserPaneControl
{
    /// <summary>Gets who may act.</summary>
    BrowserControlState Control { get; }

    /// <summary>Gets whether take-over is holding the keyboard. F6 clears it through <see cref="ReleaseControlAsync"/>.</summary>
    bool KeyboardCaptured { get; }

    /// <summary>Takes over the desktop browser and captures the keyboard.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    Task TakeOverAsync(CancellationToken cancellationToken = default);

    /// <summary>Releases keyboard capture and returns control to the agent, unless Stop already revoked it.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    Task ReleaseControlAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends one pointer event. Refused when this head is not the desktop that runs the browser.</summary>
    /// <param name="input">The pointer event.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    Task SendPointerAsync(BrowserPointerInput input, CancellationToken cancellationToken = default);

    /// <summary>Sends one key event. Refused when this head is not the desktop that runs the browser.</summary>
    /// <param name="input">The key event.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    Task SendKeyAsync(BrowserKeyInput input, CancellationToken cancellationToken = default);

    /// <summary>Cancels in-flight calls, revokes control and leaves the browser open.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the browser process.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    Task CloseBrowserAsync(CancellationToken cancellationToken = default);
}
