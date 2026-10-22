namespace OmpOS.Presentation.Runs;

/// <summary>
/// The input channel of one active run (ADE-03). The engine behind it decides how each message reaches the run. The omp RPC
/// protocol has steer and follow_up commands; the ACP path has no steer command.
/// </summary>
public interface IRunInput
{
    /// <summary>Delivers a steering message to the active run immediately.</summary>
    /// <param name="text">The steering message.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    Task SteerAsync(string text, CancellationToken cancellationToken);

    /// <summary>Starts a queued follow-up as the run's next prompt. Called only after the active run completes.</summary>
    /// <param name="text">The follow-up message.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    Task FollowUpAsync(string text, CancellationToken cancellationToken);
}
