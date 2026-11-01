namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>The only fields an artifact audit payload may carry.</summary>
/// <param name="Hash">The version hash.</param>
/// <param name="Origins">The origins.</param>
internal sealed record AuditPayload(string? Hash, IReadOnlyList<string> Origins);
