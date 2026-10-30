using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Test-only startup filter that throws for one canary path, driving the unhandled-exception
/// path through the real middleware pipeline. Registered from tests via
/// <see cref="ApiFactory"/> service configuration; never part of the shipped host.
/// </summary>
internal sealed class UnhandledExceptionStartupFilter : IStartupFilter
{
    /// <summary>Gets the test-only path that throws when requested.</summary>
    public const string BoomPath = "/__test/boom";

    /// <summary>Gets the unique exception message that must never appear in a response body.</summary>
    public const string BoomMarker = "boom-canary-7d21f4a9c0e34b6d8f1a2b3c4d5e6f70";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return app =>
        {
            next(app);
            app.Use(static async (context, nextMiddleware) =>
            {
                if (string.Equals(context.Request.Path.Value, BoomPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(BoomMarker);
                }

                await nextMiddleware().ConfigureAwait(false);
            });
        };
    }
}
