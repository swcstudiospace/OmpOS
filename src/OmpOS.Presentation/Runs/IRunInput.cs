namespace OmpOS.Presentation.Runs;

/// <summary>
/// The input channel of one active run (ADE-03). The engine behind it decides how each message reaches the run. The omp RPC
/// protocol has steer and follow_up commands; the ACP path has no steer command.
/// </summary>
public interface IRunInput
{
    /// <summary>
    /// Gets the number of steering messages the input has accepted that have not yet started their turn. An input that delivers each
    /// steer at once holds none and reports 0.
    /// </summary>
    int SteersHeld { get; }

    /// <summary>Delivers a steering message to the active run. An engine without a steer command holds it until the active turn ends.</summary>
    /// <param name="text">The steering message.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    Task SteerAsync(string text, CancellationToken cancellationToken);

    /// <summary>Starts a queued follow-up as the run's next prompt. Called only after the active run completes.</summary>
    /// <param name="text">The follow-up message.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    Task FollowUpAsync(string text, CancellationToken cancellationToken);
}
