namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// Kill-switch hotkey on the backend's own thread and operating-system connection, independent of the UI thread
/// and of the injection connection.
/// </summary>
public interface IGlobalHotkeyRegistrar
{
    /// <summary>Registers the chord. Failure to confirm registration is <see cref="DenialCode.KillSwitchUnavailable"/>.</summary>
    /// <param name="chord">The kill chord.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns><see langword="true"/> when the operating system confirmed the grab.</returns>
    ValueTask<bool> RegisterAsync(string chord, CancellationToken cancellationToken);

    /// <summary>Releases the grab.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>A completed task.</returns>
    ValueTask UnregisterAsync(CancellationToken cancellationToken);
}

