using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Test-only OIDC bearer host. Tokens are signed by a static in-test ES256 (P-256) key set as the JwtBearer issuer signing key. Discovery is replaced
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

    /// <summary>Gets the tenant-hash pepper that PostgreSQL-store test hosts configure. It is a test value, never a deployment secret.</summary>
    public const string TestTenantHashPepper = "jwt-test-tenant-hash-pepper";

    private const string KeyId = "autonogrammer-api-test-key";
    private const string HmacKeyId = "autonogrammer-api-test-hmac-key";

    private static readonly ECDsaSecurityKey SigningKey = NewEcKey(KeyId);
    private static readonly SymmetricSecurityKey HmacKey = new(Encoding.UTF8.GetBytes("published-hmac-key-material-0123456789-abcdef")) { KeyId = HmacKeyId };

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
    /// Returns the settings a production host needs to start: an HTTPS authority, an audience, the ACP engine's workspace root, and the PostgreSQL session store. The
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
            ["Harness:Acp:WorkspaceRoot"] = Path.Combine(Path.GetTempPath(), "autonogrammer-test-workspaces"),
            ["Api:Auth:ValidIssuer"] = Issuer,
            ["Sessions:Store"] = "Postgres",
            ["Sessions:Postgres:ConnectionString"] = PostgresConnectionString(),
            ["DataPlane:TenantHashPepper"] = TestTenantHashPepper,
        };
    }

    /// <summary>
    /// Returns the PostgreSQL connection string from <see cref="PostgresConnectionEnvironmentVariable"/>, or throws when it is unset. The database is migrated to the
    /// compiled head once per process first (<see cref="DataPlane.TestDatabaseMigrator"/>), because the API creates no schema objects of its own.
    /// </summary>
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

        DataPlane.TestDatabaseMigrator.EnsureMigrated(connectionString);
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
    public static void InjectStaticMetadata(IServiceCollection services) => InjectStaticMetadata(services, SigningKey);

    /// <summary>Returns a signing key that the test host does not trust: a different ES256 key under the same key id.</summary>
    /// <returns>A key whose tokens must fail signature validation.</returns>
    public static SecurityKey ForeignSigningKey()
    {
        return NewEcKey(KeyId);
    }

    /// <summary>Returns a fresh ES256 key with the given key id, for rotation tests.</summary>
    /// <param name="keyId">The key id.</param>
    /// <returns>The key.</returns>
    public static ECDsaSecurityKey NewEcKey(string keyId)
    {
        // The key and its ECDsa live for the test process: tokens signed with it are validated by hosts that outlive this call.
#pragma warning disable CA2000
        return new ECDsaSecurityKey(ECDsa.Create(ECCurve.NamedCurves.nistP256)) { KeyId = keyId };
#pragma warning restore CA2000
    }

    /// <summary>
    /// Returns a symmetric key that the test metadata publishes under its own key id, for the algorithm pin test. A token signed with it passes signature and key
    /// checks, so only the <c>ValidAlgorithms</c> pin can reject it.
    /// </summary>
    /// <returns>An HS256 key the host trusts as a key.</returns>
    public static SecurityKey HmacSigningKey()
    {
        return HmacKey;
    }

    /// <summary>Returns an unsigned token (<c>alg</c> none) with otherwise valid claims, for the signed-token requirement test.</summary>
    /// <param name="subject">The subject claim value.</param>
    /// <returns>The compact token with an empty signature.</returns>
    public static string IssueUnsignedToken(string subject)
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var expires = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var payload = Base64UrlEncoder.Encode(
            $$"""{"sub":"{{subject}}","exp":{{expires}},"iss":"{{Issuer}}","aud":"{{Audience}}","role":"authenticated"}""");
        return header + "." + payload + ".";
    }

    /// <summary>Installs static metadata that holds exactly <paramref name="key"/>, replacing the previous test key, so a host can rotate keys.</summary>
    /// <param name="services">The test host service collection.</param>
    /// <param name="key">The signing key the host trusts.</param>
    public static void InjectStaticMetadata(IServiceCollection services, SecurityKey key)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(CreateStaticMetadata(key));
            options.TokenValidationParameters.IssuerSigningKey = null;
        });
    }

    /// <summary>Issues a token for <paramref name="subject"/> signed by the test key unless another key is supplied.</summary>
    /// <param name="subject">The subject claim value.</param>
    /// <param name="audience">The audience the token is issued for.</param>
    /// <param name="signingKey">An optional alternative signing key.</param>
    /// <param name="role">The role claim value; null omits the claim. Defaults to <c>authenticated</c>.</param>
    /// <param name="anonymous">When true the token carries <c>is_anonymous</c> true.</param>
    /// <param name="issuer">The issuer; defaults to <see cref="Issuer"/>.</param>
    /// <param name="expires">The expiry; defaults to five minutes from now.</param>
    /// <param name="algorithm">The signing algorithm; defaults to ES256 for an EC key.</param>
    /// <returns>The compact serialized token.</returns>
    public static string IssueToken(
        string subject,
        string audience = Audience,
        SecurityKey? signingKey = null,
        string? role = "authenticated",
        bool anonymous = false,
        string issuer = Issuer,
        DateTime? expires = null,
        string? algorithm = null)
    {
        var claims = new List<Claim> { new("sub", subject) };
        if (role is not null)
        {
            claims.Add(new Claim("role", role));
        }

        if (anonymous)
        {
            claims.Add(new Claim("is_anonymous", "true", ClaimValueTypes.Boolean));
        }

        var key = signingKey ?? SigningKey;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, algorithm ?? (key is SymmetricSecurityKey ? SecurityAlgorithms.HmacSha256 : SecurityAlgorithms.EcdsaSha256)),
        };
        if (expires is { } past && past < DateTime.UtcNow)
        {
            descriptor.NotBefore = past.AddMinutes(-5);
        }

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static OpenIdConnectConfiguration CreateStaticMetadata(SecurityKey key)
    {
        var metadata = new OpenIdConnectConfiguration { Issuer = Issuer };
        metadata.SigningKeys.Add(key);
        metadata.SigningKeys.Add(HmacKey);
        return metadata;
    }

    /// <summary>
    /// Metadata that models an identity provider rotating its signing keys: it serves the published key set until <see cref="RequestRefresh"/>
    /// is called, when it serves the key set published meanwhile. This is what JwtBearer does on a token whose <c>kid</c> is unknown.
    /// </summary>
    internal sealed class RotatingMetadata : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private OpenIdConnectConfiguration _served;
        private OpenIdConnectConfiguration? _published;

        /// <summary>Initializes a new instance of the <see cref="RotatingMetadata"/> class serving <paramref name="key"/>.</summary>
        /// <param name="key">The initially served key.</param>
        public RotatingMetadata(SecurityKey key)
        {
            _served = CreateStaticMetadata(key);
        }

        /// <summary>Gets how many times a refresh was requested.</summary>
        public int RefreshCount { get; private set; }

        /// <summary>Publishes a new key set that is served only after the next refresh request.</summary>
        /// <param name="key">The newly published key.</param>
        public void Publish(SecurityKey key) => _published = CreateStaticMetadata(key);

        /// <inheritdoc />
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) => Task.FromResult(_served);

        /// <inheritdoc />
        public void RequestRefresh()
        {
            RefreshCount++;
            if (_published is not null)
            {
                _served = _published;
                _published = null;
            }
        }
    }

    /// <summary>Installs <paramref name="metadata"/> as the JwtBearer configuration manager.</summary>
    /// <param name="services">The test host service collection.</param>
    /// <param name="metadata">The rotating metadata.</param>
    public static void InjectRotatingMetadata(IServiceCollection services, RotatingMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(metadata);
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.ConfigurationManager = metadata;
            options.TokenValidationParameters.IssuerSigningKey = null;
        });
    }
}
