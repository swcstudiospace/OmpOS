namespace Autonogrammer.Presentation.Settings;

/// <summary>One line of the diagnostics result, with the words the view draws and the sentence a screen reader speaks.</summary>
/// <param name="Name">The provider the endpoint belongs to.</param>
/// <param name="Host">The endpoint as host or host:port, or the words that say it cannot be shown.</param>
/// <param name="StatusLabel">The word for the status.</param>
/// <param name="Detail">What happened, with the HTTP status and the time it took when there are any.</param>
/// <param name="Summary">The whole line as one sentence for the accessible name.</param>
/// <param name="Status">The status the words stand for.</param>
public sealed record NetworkCheckRow(string Name, string Host, string StatusLabel, string Detail, string Summary, EndpointStatus Status);
