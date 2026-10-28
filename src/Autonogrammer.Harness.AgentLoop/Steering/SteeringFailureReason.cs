namespace Autonogrammer.Harness.AgentLoop.Steering;

/// <summary>Why a directive could not be put into the steering inbox.</summary>
public enum SteeringFailureReason
{
    /// <summary>The session's omp process does not answer: its heartbeat is missing, unreadable or older than 15 seconds, or its inbox cannot be written.</summary>
    NotServing = 0,

    /// <summary>The session has no steering directory: omp was never started for it.</summary>
    NoSteerDirectory = 1,

    /// <summary>The directive is not valid: blank, too long or an unknown mode.</summary>
    InvalidDirective = 2,

    /// <summary>64 directives are already waiting in the inbox.</summary>
    QueueFull = 3,
}
