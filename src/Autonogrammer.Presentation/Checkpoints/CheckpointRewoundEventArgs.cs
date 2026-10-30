namespace Autonogrammer.Presentation.Checkpoints;

/// <summary>Tells that a rewind wrote files, so the head can refresh the Changes bar and any open editor.</summary>
public sealed class CheckpointRewoundEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="outcome">The completed rewind.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    public CheckpointRewoundEventArgs(CheckpointRewindOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        Outcome = outcome;
    }

    /// <summary>Gets the completed rewind.</summary>
    public CheckpointRewindOutcome Outcome { get; }
}
