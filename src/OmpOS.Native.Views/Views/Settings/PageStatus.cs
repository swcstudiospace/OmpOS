namespace OmpOS.Native.Views.Settings;

/// <summary>What a settings page shows, with the words for it.</summary>
/// <param name="Kind">The state.</param>
/// <param name="Heading">The heading of the failed-load state; empty for every other state.</param>
/// <param name="Message">The loading text or the reason the load failed; empty while the page is ready.</param>
internal readonly record struct PageStatus(PageStatusKind Kind, string Heading, string Message);
