namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>One recorded audit event. The payload has no title, content or account id.</summary>
/// <param name="Kind">The closed kind.</param>
/// <param name="ArtifactId">The artifact identifier.</param>
/// <param name="Payload">The JSON payload.</param>
internal sealed record ArtifactAuditEvent(string Kind, string ArtifactId, string Payload);
