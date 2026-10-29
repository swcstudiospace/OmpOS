using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>Test host helpers for owner-token mode: a fixed token, its digest, configuration, and a per-request remote address.</summary>
internal static class OwnerTokenTestHost
{
    /// <summary>Gets the owner token the host accepts.</summary>
    public const string Token = "owner-token-for-tests-0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>Gets a second token that rotation accepts.</summary>
    public const string RotatedToken = "rotated-owner-token-for-tests-9876543210zyxwvutsrqponmlkjihgfedcba";

    /// <summary>Gets the request header the test pipeline reads the simulated remote address from.</summary>
    public const string RemoteAddressHeader = "X-Test-Remote-Address";

    /// <summary>Returns the lowercase-hex SHA-256 of <paramref name="token"/>.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The digest.</returns>
    public static string Digest(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Returns the configuration of a Development owner-token host.</summary>
    /// <param name="maxFailures">The failed attempts allowed per minute.</param>
    /// <returns>The configuration overrides.</returns>
    public static Dictionary<string, string?> Config(int maxFailures = 10) => new(StringComparer.Ordinal)
    {
        ["Api:Auth:Mode"] = "OwnerToken",
        ["Api:Auth:AllowDevelopmentPrincipal"] = "false",
        ["Api:Auth:OwnerToken:Sha256:0"] = Digest(Token),
        ["Api:Auth:OwnerToken:Subject"] = "operator",
        ["Api:Auth:OwnerToken:MaxFailuresPerMinute"] = maxFailures.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>Creates an owner-token host.</summary>
    /// <param name="config">The configuration; defaults to <see cref="Config"/>.</param>
    /// <param name="logs">An optional capturing log provider.</param>
    /// <returns>The factory.</returns>
    public static ApiFactory CreateFactory(Dictionary<string, string?>? config = null, ILoggerProvider? logs = null) =>
        new(config ?? Config(), static services => services.AddTransient<IStartupFilter, RemoteAddressFilter>(), logs);

    /// <summary>Sends <c>GET /sessions</c> with an Authorization header value and an optional simulated remote address.</summary>
    /// <param name="client">The client.</param>
    /// <param name="authorization">The raw header value, or null for none.</param>
    /// <param name="address">The simulated remote address, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response.</returns>
    public static async Task<HttpResponseMessage> GetSessionsAsync(HttpClient client, string? authorization, string? address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/sessions", UriKind.Relative));
        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        if (address is not null)
        {
            request.Headers.TryAddWithoutValidation(RemoteAddressHeader, address);
        }

        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sets the bearer header of <paramref name="client"/>.</summary>
    /// <param name="client">The client.</param>
    /// <param name="token">The bearer token.</param>
    public static void UseBearer(HttpClient client, string token)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Copies the simulated address header into the connection's remote address, first in the pipeline.</summary>
    private sealed class RemoteAddressFilter : IStartupFilter
    {
        /// <inheritdoc />
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next)
        {
            ArgumentNullException.ThrowIfNull(next);
            return app =>
            {
                app.Use(async (context, nextDelegate) =>
                {
                    if (context.Request.Headers.TryGetValue(RemoteAddressHeader, out var value) && IPAddress.TryParse(value.ToString(), out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    await nextDelegate(context).ConfigureAwait(false);
                });
                next(app);
            };
        }
    }
}
