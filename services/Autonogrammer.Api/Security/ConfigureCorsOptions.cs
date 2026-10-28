using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Autonogrammer.Api.Options;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Builds the locked CORS policy from <see cref="ApiOptions"/> when the CORS subsystem
/// first resolves it, so configuration applied after startup (including test overrides)
/// is honored. Empty origins deny all cross-origin traffic.
/// </summary>
internal sealed class ConfigureCorsOptions : IConfigureOptions<CorsOptions>
{
    /// <summary>Gets the policy name.</summary>
    public const string PolicyName = "Autonogrammer";

    private readonly IOptions<ApiOptions> _api;

    /// <summary>Initializes a new instance of the <see cref="ConfigureCorsOptions"/> class.</summary>
    /// <param name="api">The API options carrying the allowed origins.</param>
    public ConfigureCorsOptions(IOptions<ApiOptions> api)
    {
        _api = api;
    }

    /// <inheritdoc />
    public void Configure(CorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var origins = _api.Value.Cors.Origins;
        options.AddPolicy(
            PolicyName,
            policy =>
            {
                if (origins.Count == 0)
                {
                    policy.WithMethods("GET", "POST");
                }
                else
                {
                    policy.WithOrigins(origins.ToArray()).AllowAnyHeader().AllowAnyMethod();
                }
            });
    }
}
