namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>What the decision engine says about one tool call.</summary>
public enum RunModeOutcome
{
    /// <summary>The call waits for the user. The default, and what an unknown call gets.</summary>
    Ask = 0,

    /// <summary>The call runs without asking.</summary>
    Allow = 1,

    /// <summary>The call is refused without asking.</summary>
    Deny = 2,
}
