using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>Judges the lifetime of a test token on the injected clock, the one the token was issued by, instead of the machine's clock.</summary>
internal static class TestTokenLifetime
{
    /// <summary>Creates a validator that accepts a token whose window contains the clock's current time.</summary>
    /// <param name="clock">The clock the tokens were issued by.</param>
    /// <returns>The validator, for <see cref="TokenValidationParameters.LifetimeValidator"/>.</returns>
    public static LifetimeValidator Of(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return (notBefore, expires, _, _) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return (notBefore is null || notBefore <= now) && (expires is not null && expires >= now);
        };
    }
}
