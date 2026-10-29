namespace Autonogrammer.Api.Security;

/// <summary>
/// Derives the opaque partition key the per-caller rate limiters bucket on: the authenticated <c>sub</c> claim when the request
/// carries one, else the remote address, else <c>anonymous</c>. Key length is bounded, so an oversized subject cannot inflate
/// limiter memory. Memory per policy stays bounded because the framework's partitioned limiter disposes a partition's limiter once
/// it has been idle and fully replenished; an idle key therefore costs nothing after one replenishment period.
/// </summary>
internal static class RateLimitPartitionKey
{
    /// <summary>The longest key returned, in characters.</summary>
    public const int MaxLength = 128;

    /// <summary>The key of a request with neither a subject nor a remote address.</summary>
    public const string Anonymous = "anonymous";

    /// <summary>Returns the partition key of a request.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The bounded key, prefixed with its kind (<c>sub:</c> or <c>ip:</c>) so a subject can never collide with an address.</returns>
    public static string For(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var subject = context.User.FindFirst(JwtBearerSetup.SubjectClaimType)?.Value;
        if (!string.IsNullOrEmpty(subject))
        {
            return Bound("sub:" + subject);
        }

        var address = context.Connection.RemoteIpAddress;
        return address is null ? Anonymous : Bound("ip:" + address.ToString());
    }

    /// <summary>Returns the key for the remote address alone, ignoring any subject.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The bounded address key, or <see cref="Anonymous"/>.</returns>
    public static string ForAddress(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var address = context.Connection.RemoteIpAddress;
        return address is null ? Anonymous : Bound("ip:" + address.ToString());
    }

    private static string Bound(string key) =>
        key.Length <= MaxLength ? key : key[..MaxLength];
}
