namespace Autonogrammer.Api.Security;

/// <summary>
/// Development-only <see cref="IUserContext"/>. It is registered solely behind the explicit <c>Api:Auth:AllowDevelopmentPrincipal</c>
/// opt-in, which is refused outside the Development environment. Every request is the same principal. Hosts that do not opt in name
/// the caller from the subject claim of a validated bearer token instead.
/// </summary>
internal sealed class SingleDevPrincipal : IUserContext
{
    /// <summary>Gets the fixed local-developer principal name.</summary>
    public const string DevName = "local-dev";

    /// <inheritdoc />
    public string Name => DevName;
}
