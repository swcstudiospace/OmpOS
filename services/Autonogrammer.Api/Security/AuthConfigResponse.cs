using System.Text.Json.Serialization;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Public sign-in configuration answered by <c>GET /auth/config</c>. It carries no secret.
/// </summary>
/// <param name="Mode">How the client authenticates: <c>owner-token</c>, <c>oidc</c> or <c>development</c>.</param>
/// <param name="Authority">The OIDC authority in <c>oidc</c> mode, otherwise null.</param>
/// <param name="Audience">The OIDC audience in <c>oidc</c> mode, otherwise null.</param>
internal sealed record AuthConfigResponse(
    string Mode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Authority,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Audience);
