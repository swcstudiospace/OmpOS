#pragma warning disable MA0048 // The plan owns this file for the control enum and its chrome sentence.
namespace Autonogrammer.Contracts.Cowork.Browser;

/// <summary>Who may act on the browser. Revoked holds until the user resumes.</summary>
public enum BrowserControlState
{
    /// <summary>The agent may call tools.</summary>
    Agent,

    /// <summary>The user has taken over. Agent calls wait.</summary>
    User,

    /// <summary>Stop revoked control. The browser stays open.</summary>
    Revoked,
}

/// <summary>
/// Chrome copy that lives outside the page frame. A page cannot imitate it because it is not part of <see cref="BrowserFrame"/>.
/// </summary>
public static class BrowserChromeCopy
{
    /// <summary>The sentence shown while the agent can actuate the browser.</summary>
    public const string AgentControlling = "The agent is controlling the browser";
}
