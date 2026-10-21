namespace OmpOS.EngineHost;

/// <summary>
/// Persists and restores the native window geometry. v1 ships a no-op default
/// (<see cref="NoOpWindowStateStore"/>); per-platform backends are a later shell task.
/// </summary>
public interface IWindowStateStore
{
    /// <summary>Loads the last saved window state, or null when none was saved.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    Task<WindowState?> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Saves the current window state.</summary>
    /// <param name="state">The state to save.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    Task SaveAsync(WindowState state, CancellationToken cancellationToken);
}
