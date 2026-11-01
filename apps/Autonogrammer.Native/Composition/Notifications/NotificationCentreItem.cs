namespace Autonogrammer.Native.Composition.Notifications;

/// <summary>One in-app centre item. OS text stays title-only unless <paramref name="ShowContent"/> is set.</summary>
/// <param name="Kind">The kind name. Scheduled-task kinds are not produced here.</param>
/// <param name="Title">The title. This is what an OS sink would show by default.</param>
/// <param name="SessionId">The session, or null.</param>
/// <param name="ShowContent">True only when the user asked to show content. The default is title-only.</param>
internal sealed record NotificationCentreItem(string Kind, string Title, string? SessionId, bool ShowContent);
