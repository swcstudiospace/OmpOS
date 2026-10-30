#pragma warning disable MA0048 // The sink contract and its receipt share this plan-owned file.
namespace Autonogrammer.Harness.Notifications;

/// <summary>
/// Fans a <see cref="NotificationRequest"/> out to the in-app centre and, when policy allows, the operating-system surface.
/// Title only is the default. Callers pass an explicit Show content flag.
/// </summary>
/// <remarks>
/// Presentation does not reference this assembly. The in-app centre bridge that implements this contract is composed in Native, not here.
/// </remarks>
public interface INotificationSink
{
    /// <summary>Records the request in the in-app centre and decides whether the operating-system surface may show it.</summary>
    /// <param name="request">The notification.</param>
    /// <param name="windowFocused">True when the application window is focused. A focused window suppresses the operating-system surface.</param>
    /// <param name="doNotDisturb">True when Do-not-disturb is on. That suppresses the operating-system surface.</param>
    /// <param name="showContent">True to reveal the summary on the operating-system surface. False, the default, is title only.</param>
    /// <returns>What was kept and what the operating-system surface may show.</returns>
    NotificationReceipt Publish(NotificationRequest request, bool windowFocused, bool doNotDisturb, bool showContent = false);
}

/// <summary>The result of fanning one request out. The in-app centre keeps every request; the operating-system surface may not.</summary>
/// <param name="RetainedInApp">True when the in-app centre kept the request. This is always true.</param>
/// <param name="DeliverToOperatingSystem">True when the operating-system surface should show this request.</param>
/// <param name="Suppression">Why the operating-system surface was suppressed, or null when it was not suppressed for focus or Do-not-disturb.</param>
/// <param name="OperatingSystemDrops">How many requests the rate cap has dropped from the operating-system surface.</param>
/// <param name="OperatingSystemTitle">The title the operating-system surface may show.</param>
/// <param name="OperatingSystemSummary">The summary the operating-system surface may show, or null when the surface is title only.</param>
public sealed record NotificationReceipt(
    bool RetainedInApp,
    bool DeliverToOperatingSystem,
    string? Suppression,
    int OperatingSystemDrops,
    string OperatingSystemTitle,
    string? OperatingSystemSummary);
