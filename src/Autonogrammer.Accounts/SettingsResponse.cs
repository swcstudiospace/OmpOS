namespace Autonogrammer.Accounts;

/// <summary>The part of the <c>GET /settings</c> answer this client reads. The service adds fields; they are skipped.</summary>
/// <param name="External">The enabled flag of each external provider name, or null when the answer has none.</param>
internal sealed record SettingsResponse(Dictionary<string, bool>? External);
