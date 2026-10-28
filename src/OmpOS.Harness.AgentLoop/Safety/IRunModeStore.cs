namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>Where the chosen run mode is kept between launches.</summary>
public interface IRunModeStore
{
    /// <summary>Reads the chosen mode. A missing or unreadable store gives <see cref="RunModes.Default"/>.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The mode.</returns>
    Task<RunMode> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Keeps a mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<SafetyEditResult> SaveAsync(RunMode mode, CancellationToken cancellationToken);
}
