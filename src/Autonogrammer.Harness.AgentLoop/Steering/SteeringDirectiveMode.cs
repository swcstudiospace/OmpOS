namespace Autonogrammer.Harness.AgentLoop.Steering;

/// <summary>How the omp plugin hands a steering directive to the running session. The wire names are <c>steer</c>, <c>followUp</c> and <c>aside</c>.</summary>
public enum SteeringDirectiveMode
{
    /// <summary>Queued at the next boundary of the running turn (omp <c>deliverAs: "steer"</c>).</summary>
    Steer = 0,

    /// <summary>Runs after the current turn ends (omp <c>deliverAs: "followUp"</c>).</summary>
    FollowUp = 1,

    /// <summary>Injected at the next step boundary without interrupting a running tool batch (omp <c>deliverAs: "aside"</c>).</summary>
    Aside = 2,
}
