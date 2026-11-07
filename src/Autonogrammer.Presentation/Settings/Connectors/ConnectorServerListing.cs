namespace Autonogrammer.Presentation.Settings.Connectors;

/// <summary>A server the matrix can show before a check.</summary>
/// <param name="ServerId">The server id.</param>
/// <param name="DisplayName">The name shown on the card.</param>
public sealed record ConnectorServerListing(string ServerId, string DisplayName);
