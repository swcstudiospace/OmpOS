namespace Autonogrammer.Harness.BrowserUse.Policy;

/// <summary>
/// A user decision that one local dev server may be reached. Scope is the owning session. Always is not representable.
/// </summary>
/// <param name="Host">The host. Only localhost, 127.0.0.1, or [::1].</param>
/// <param name="Port">The explicit port.</param>
public sealed record LocalOriginGrant(string Host, int Port);
