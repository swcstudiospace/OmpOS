namespace Autonogrammer.Harness.Automations;

/// <summary>
/// The work an automation performs. It must observe <paramref name="cancellationToken"/>: when a run exceeds its runtime
/// bound or is cancelled, the engine cancels that token and waits for the body to return.
/// </summary>
/// <param name="context">The run's context, used to emit events.</param>
/// <param name="cancellationToken">Cancelled when the run exceeds its runtime bound or the caller cancels it.</param>
public delegate Task AutomationBody(AutomationRunContext context, CancellationToken cancellationToken);
