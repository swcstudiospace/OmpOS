using Autonogrammer.Contracts.Cowork.Browser;

namespace Autonogrammer.Harness.BrowserUse.Viewer;

/// <summary>
/// Application chrome outside the page frame. Stop and Close are properties of this state, not of the page image.
/// </summary>
/// <param name="Sentence">The sentence the chrome shows.</param>
/// <param name="ShowStop">True when Stop is offered.</param>
/// <param name="ShowClose">True when Close browser is offered.</param>
/// <param name="InsidePageFrame">Always false. A page must not be able to imitate this indicator.</param>
public sealed record BrowserIndicatorState(string Sentence, bool ShowStop, bool ShowClose, bool InsidePageFrame)
{
    /// <summary>Builds the chrome indicator for <paramref name="control"/>.</summary>
    /// <param name="control">Who may act.</param>
    /// <returns>The indicator. It is never inside the page frame.</returns>
    public static BrowserIndicatorState For(BrowserControlState control)
    {
        return new BrowserIndicatorState(
            BrowserChromeCopy.AgentControlling,
            ShowStop: control != BrowserControlState.Revoked,
            ShowClose: true,
            InsidePageFrame: false);
    }
}
