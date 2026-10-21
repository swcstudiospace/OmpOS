namespace OmpOS.Api.Security;

/// <summary>
/// No-op <see cref="IUserContext"/> for local single-developer use. Every request is the
/// same principal; session ownership still records the name so a future multi-user
/// provider can take over the same field. This is the documented auth extension point.
/// </summary>
internal sealed class SingleDevPrincipal : IUserContext
{
    /// <summary>Gets the fixed local-developer principal name.</summary>
    public const string DevName = "local-dev";

    /// <inheritdoc />
    public string Name => DevName;
}
