namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>
/// The seam 72-04's BrowserCallGate fills. Stop cancels in-flight calls. Take-over waits.
/// The provider never references the pane views.
/// </summary>
public interface IBrowserCallGate
{
    /// <summary>Gets the token Stop cancels.</summary>
    CancellationToken Cancellation { get; }

    /// <summary>Gets whether the user has taken over.</summary>
    bool UserHasControl { get; }

    /// <summary>Waits while the user has control.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The task.</returns>
    Task WaitIfTakenOverAsync(CancellationToken cancellationToken);
}
