namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Payload for <see cref="IKillSwitchControl.StateChanged"/>.</summary>
public sealed class KillSwitchStateChangedEventArgs : EventArgs
{
    /// <summary>Creates the payload.</summary>
    /// <param name="state">The new state.</param>
    public KillSwitchStateChangedEventArgs(KillSwitchState state)
    {
        State = state;
    }

    /// <summary>The new state.</summary>
    public KillSwitchState State { get; }
}
