using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Accounts;

/// <summary>
/// Public sign-in configuration answered by <c>GET /auth/config</c>. It carries no secret: the publishable key is public by design. The first three
/// members are the answer the endpoint has always given; the rest are null, and so omitted from the JSON, unless the OIDC client block is configured.
/// </summary>
/// <param name="Mode">How the client authenticates: <c>owner-token</c>, <c>oidc</c> or <c>development</c>.</param>
/// <param name="Authority">The OIDC authority in <c>oidc</c> mode, otherwise null (written as JSON null).</param>
/// <param name="Audience">The OIDC audience in <c>oidc</c> mode, otherwise null (written as JSON null).</param>
/// <param name="ProjectUrl">The https project URL of the auth host, or null when no client block is configured.</param>
/// <param name="PublishableKey">The publishable key sent as <c>apikey</c>, or null when no client block is configured.</param>
/// <param name="Methods">The offered sign-in methods, or null when no client block is configured.</param>
/// <param name="EmailCode">The email code timing, or null when no client block is configured.</param>
/// <param name="SignupEmailHasCode">True when the first-time signup email already shows a code (owner action O4); null when no client block is configured.</param>
public sealed record AuthConfigResponse(
    string Mode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Authority,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Audience,
    Uri? ProjectUrl = null,
    string? PublishableKey = null,
    AuthMethods? Methods = null,
    AuthEmailCode? EmailCode = null,
    bool? SignupEmailHasCode = null);
