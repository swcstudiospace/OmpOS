namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>How a message sent while a run is active reaches it.</summary>
public enum SteeringMode
{
    /// <summary>A steering message: handed to the active run at once, or held by an engine without a steer command until the active turn ends.</summary>
    Steer = 0,

    /// <summary>A follow-up: queued, then started as the run's next prompt after the active run completes.</summary>
    FollowUp = 1,
}
