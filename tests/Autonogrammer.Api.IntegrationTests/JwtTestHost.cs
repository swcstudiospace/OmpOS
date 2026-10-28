using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Test-only OIDC bearer host. Tokens are signed by a static HMAC key set as the JwtBearer issuer signing key. Discovery is replaced
/// by a static configuration manager, so validation never fetches metadata from an identity provider and no request leaves the process.
/// Production-shaped hosts name <see cref="Issuer"/> as their authority, which the static manager answers in place of discovery.
/// </summary>
internal static class JwtTestHost
{
    /// <summary>Gets the audience the test host requires.</summary>
    public const string Audience = "autonogrammer-api-tests";

    /// <summary>Gets the issuer of test tokens and the HTTPS authority of production-shaped hosts.</summary>
    public const string Issuer = "https://issuer.test.invalid/";

    /// <summary>Gets the environment variable that holds the PostgreSQL connection string for Production-shaped hosts. Its name matches the PostgreSQL test fixture.</summary>
    public const string PostgresConnectionEnvironmentVariable = "AUTONOGRAMMER_TEST_POSTGRES_CONNECTION";

    private const string KeyId = "autonogrammer-api-test-key";

    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("autonogrammer-api-test-signing-key-material-0123456789")) { KeyId = KeyId };

    /// <summary>Returns the settings of a Development host with JWT validation on and the development principal off.</summary>
    /// <returns>The configuration overrides.</returns>
    public static Dictionary<string, string?> DevelopmentConfig()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Api:Auth:Audience"] = Audience,
            ["Api:Auth:ValidIssuer"] = Issuer,
            ["Api:Auth:AllowDevelopmentPrincipal"] = "false",
        };
    }

    /// <summary>
    /// Returns the settings a production host needs to start: an HTTPS authority, an audience, and the PostgreSQL session store. The
    /// store's connection string is read from <see cref="PostgresConnectionEnvironmentVariable"/>, so the test run must set it.
    /// </summary>
    /// <returns>The configuration overrides.</returns>
    /// <exception cref="InvalidOperationException">The PostgreSQL connection environment variable is not set.</exception>
    public static Dictionary<string, string?> ProductionConfig()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Api:Auth:Authority"] = Issuer,
            ["Api:Auth:Audience"] = Audience,
            ["Harness:Adapter"] = "Acp",
            ["Api:Auth:ValidIssuer"] = Issuer,
            ["Sessions:Store"] = "Postgres",
            ["Sessions:Postgres:ConnectionString"] = PostgresConnectionString(),
        };
    }

    /// <summary>Returns the PostgreSQL connection string from <see cref="PostgresConnectionEnvironmentVariable"/>, or throws when it is unset.</summary>
    /// <returns>The connection string.</returns>
    internal static string PostgresConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(PostgresConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{PostgresConnectionEnvironmentVariable} is not set. Production-shaped test hosts use the PostgreSQL session store, " +
                "so the test run must set that variable to a PostgreSQL connection string; the gate sets it.");
        }

        return connectionString;
    }

    /// <summary>Creates a Development host that validates bearer tokens against the static test key.</summary>
    /// <returns>The host factory.</returns>
    public static ApiFactory CreateFactory()
    {
        return new ApiFactory(DevelopmentConfig(), InjectStaticMetadata);
    }

    /// <summary>Installs the static test signing key and static metadata into the JwtBearer scheme.</summary>
    /// <param name="services">The test host service collection.</param>
    public static void InjectStaticMetadata(IServiceCollection services)
    {
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, static options =>
        {
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(CreateStaticMetadata());
            options.TokenValidationParameters.IssuerSigningKey = SigningKey;
        });
    }

    /// <summary>Returns a signing key that the test host does not trust.</summary>
    /// <returns>A key whose tokens must fail signature validation.</returns>
    public static SecurityKey ForeignSigningKey()
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes("attacker-controlled-key-material-0123456789")) { KeyId = KeyId };
    }

    /// <summary>Issues a token for <paramref name="subject"/> signed by the test key unless another key is supplied.</summary>
    /// <param name="subject">The subject claim value.</param>
    /// <param name="audience">The audience the token is issued for.</param>
    /// <param name="signingKey">An optional alternative signing key.</param>
    /// <returns>The compact serialized token.</returns>
    public static string IssueToken(string subject, string audience = Audience, SecurityKey? signingKey = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([new Claim("sub", subject)]),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static OpenIdConnectConfiguration CreateStaticMetadata()
    {
        var metadata = new OpenIdConnectConfiguration { Issuer = Issuer };
        metadata.SigningKeys.Add(SigningKey);
        return metadata;
    }
}
