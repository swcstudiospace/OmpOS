using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Test host factory: default in-memory adapter (no model calls), per-instance config
/// overrides, service replacements, and an optional capturing log provider.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _config;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly ILoggerProvider? _loggerProvider;
    private readonly string? _environment;

    /// <summary>Initializes a new instance of the <see cref="ApiFactory"/> class.</summary>
    /// <param name="config">Configuration overrides applied last (highest precedence).</param>
    /// <param name="configureServices">Service replacements applied on top of the app's services.</param>
    /// <param name="loggerProvider">Optional capturing log provider.</param>
    /// <param name="environment">Optional host environment name (defaults to Development).</param>
    public ApiFactory(
        Dictionary<string, string?>? config = null,
        Action<IServiceCollection>? configureServices = null,
        ILoggerProvider? loggerProvider = null,
        string? environment = null)
    {
        _config = config ?? [];
        _configureServices = configureServices;
        _loggerProvider = loggerProvider;
        _environment = environment;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (_environment is not null)
        {
            builder.UseEnvironment(_environment);
        }

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(EffectiveConfig()));
        if (_configureServices is not null)
        {
            builder.ConfigureTestServices(_configureServices);
        }

        if (_loggerProvider is not null)
        {
            builder.ConfigureLogging(logging => logging.AddProvider(_loggerProvider));
        }
    }

    /// <summary>
    /// Development hosts get the development principal through its explicit opt-in, so tests that send unauthenticated requests keep
    /// working. Other environments never get that opt-in; they admit any authenticated user instead, so the OIDC allow-list rule does not
    /// stop fixtures that test something else. The request timeout is long (the product default is 30 seconds) so that a loaded machine
    /// cannot cancel a slow first request such as the OpenAPI document; the timeout itself is tested with its own host. Per-test overrides win.
    /// </summary>
    private Dictionary<string, string?> EffectiveConfig()
    {
        var effective = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Hosting:RequestTimeoutSeconds"] = "600" };
        if (_environment is null || string.Equals(_environment, Environments.Development, StringComparison.Ordinal))
        {
            effective["Api:Auth:AllowDevelopmentPrincipal"] = "true";
        }
        else
        {
            // An OIDC deployment outside Development must name who may use it; fixtures that are not about the allow-list admit any valid token.
            effective["Api:Auth:AllowAnyAuthenticatedUser"] = "true";
        }

        foreach (var (key, value) in _config)
        {
            effective[key] = value;
        }

        return effective;
    }
}
