namespace Autonogrammer.Presentation.Computer;

/// <summary>Turns availability and permission states into a guidance panel and polls while the panel is open.</summary>
public interface IComputerPermissionAdvisor
{
    /// <summary>Gets the current panel.</summary>
    ComputerPermissionPanel Current { get; }

    /// <summary>Gets a value indicating whether the one-second poll is running.</summary>
    bool IsPolling { get; }

    /// <summary>Opens the panel and polls every second for at most five minutes.</summary>
    void Open();

    /// <summary>Closes the panel and stops the poll.</summary>
    void Close();

    /// <summary>Reads the status source once and replaces <see cref="Current"/>.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A completed task.</returns>
    ValueTask RefreshAsync(CancellationToken cancellationToken);
}
