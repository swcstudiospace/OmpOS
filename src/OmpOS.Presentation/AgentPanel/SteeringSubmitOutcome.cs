namespace OmpOS.Presentation.AgentPanel;

/// <summary>The result of <see cref="SteeringInputModel.SubmitAsync(string, SteeringMode, CancellationToken)"/>.</summary>
public enum SteeringSubmitOutcome
{
    /// <summary>The message was queued and appears in the steering history.</summary>
    Accepted = 0,

    /// <summary>The text was blank, so nothing happened.</summary>
    Ignored = 1,

    /// <summary>No run is active, so the text must be sent as a new prompt instead.</summary>
    NoActiveRun = 2,

    /// <summary>The queue holds <see cref="Runs.RunInputScheduler.MaxQueuedMessages"/> messages. <see cref="SteeringInputModel.RefusalText"/> says so and the draft should be kept.</summary>
    QueueFull = 3,

    /// <summary>A live steering channel did not deliver the message. It appears in the steering history as failed and the draft should be kept.</summary>
    Failed = 4,
}
