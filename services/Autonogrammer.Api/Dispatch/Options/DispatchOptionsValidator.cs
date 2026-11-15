using System.Security.Cryptography;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.Options;
using Autonogrammer.Sessions.Postgres;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Validates <see cref="DispatchOptions"/> on start. Nothing else is checked while dispatch is off, so design-time OpenAPI generation
/// with the defaults succeeds. Outside Development an enabled dispatch also needs every durable dependency and an explicit
/// <c>Dispatch:AllowProduction</c>. Failures name keys and never a secret.
/// </summary>
internal sealed class DispatchOptionsValidator : IValidateOptions<DispatchOptions>
{
    /// <summary>The longest credential lifetime.</summary>
    public static readonly TimeSpan MaxCredentialLifetime = TimeSpan.FromDays(90);

    /// <summary>The longest queue time.</summary>
    public static readonly TimeSpan MaxQueueTtl = TimeSpan.FromMinutes(5);

    /// <summary>The longest clock skew.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(2);

    /// <summary>The largest payload.</summary>
    public const int MaxPayloadBytes = 262_144;

    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IOptions<ApiAuthOptions> _auth;

    /// <summary>Initializes a new instance of the <see cref="DispatchOptionsValidator"/> class.</summary>
    /// <param name="environment">The host environment.</param>
    /// <param name="configuration">Configuration used for cross-section keys.</param>
    /// <param name="auth">The authentication options.</param>
    public DispatchOptionsValidator(IHostEnvironment environment, IConfiguration configuration, IOptions<ApiAuthOptions> auth)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(auth);
        _environment = environment;
        _configuration = configuration;
        _auth = auth;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DispatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (_auth.Value.Mode == ApiAuthMode.OwnerToken)
        {
            failures.Add("Dispatch:Enabled requires Api:Auth:Mode=Oidc.");
        }

        AddBoundChecks(options, failures);
        AddReplicaChecks(options, failures);
        AddStoreChecks(options, failures);
        AddProductionChecks(options, failures);
        if (!string.IsNullOrWhiteSpace(options.HubOrigin) && !IsHubOrigin(options.HubOrigin, !_environment.IsDevelopment()))
        {
            failures.Add("Dispatch:HubOrigin must be an absolute origin with a scheme and host and no path; https outside Development.");
        }

        AddKeyChecks(options, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void AddBoundChecks(DispatchOptions options, List<string> failures)
    {
        if (options.Credentials.Lifetime <= TimeSpan.Zero || options.Credentials.Lifetime > MaxCredentialLifetime)
        {
            failures.Add("Dispatch:Credentials:Lifetime must be positive and at most 90 days.");
        }

        if (options.QueueTtl <= TimeSpan.Zero || options.QueueTtl > MaxQueueTtl)
        {
            failures.Add("Dispatch:QueueTtl must be positive and at most 5 minutes.");
        }

        if (options.ClockSkew < TimeSpan.Zero || options.ClockSkew > MaxClockSkew)
        {
            failures.Add("Dispatch:ClockSkew must be at most 2 minutes.");
        }

        if (options.MaxPayloadBytes is < 1 or > MaxPayloadBytes)
        {
            failures.Add("Dispatch:MaxPayloadBytes must be between 1 and 262144.");
        }

        if (options.Pairing.MaxSignInAge <= TimeSpan.Zero)
        {
            failures.Add("Dispatch:Pairing:MaxSignInAge must be positive.");
        }

        if (options.Limits.MaxQueuedPerDevice < 1)
        {
            failures.Add("Dispatch:Limits:MaxQueuedPerDevice must be at least 1.");
        }

        if (options.Limits.DevicesPerAccount < 1)
        {
            failures.Add("Dispatch:Limits:DevicesPerAccount must be at least 1.");
        }
    }

    private void AddReplicaChecks(DispatchOptions options, List<string> failures)
    {
        if (!Enum.IsDefined(options.ReplicaMode))
        {
            failures.Add("Dispatch:ReplicaMode must be Single or Multi.");
        }
        else if (options.ReplicaMode == DispatchOptions.DispatchReplicaMode.Multi && !HasDurableCoordination())
        {
            failures.Add("Dispatch:ReplicaMode=Multi requires Coordination:Store=Dragonfly with Coordination:Dragonfly:ConnectionString and Sessions:Store=Postgres with Sessions:Postgres:ConnectionString.");
        }
    }

    private void AddStoreChecks(DispatchOptions options, List<string> failures)
    {
        if (!Enum.IsDefined(options.Store))
        {
            failures.Add("Dispatch:Store must be InMemory or Postgres.");
        }
        else if (options.Store == DispatchOptions.DispatchStoreKind.Postgres && !HasSessionsConnectionString())
        {
            failures.Add("Dispatch:Store=Postgres requires Sessions:Postgres:ConnectionString.");
        }
    }

    /// <summary>Adds the checks that apply only outside Development: the explicit opt-in, the durable store, the replica mode and the hub origin.</summary>
    private void AddProductionChecks(DispatchOptions options, List<string> failures)
    {
        if (_environment.IsDevelopment())
        {
            return;
        }

        if (!options.AllowProduction)
        {
            failures.Add("Dispatch:AllowProduction must be true outside Development to start dispatch.");
        }

        if (options.Store != DispatchOptions.DispatchStoreKind.Postgres)
        {
            failures.Add("Dispatch:Store must be Postgres outside Development.");
        }

        if (options.ReplicaMode != DispatchOptions.DispatchReplicaMode.Single)
        {
            failures.Add("Dispatch:ReplicaMode must be Single outside Development.");
        }

        if (string.IsNullOrWhiteSpace(options.HubOrigin))
        {
            failures.Add("Dispatch:HubOrigin is required outside Development. The origin comes from configuration, never from a request header.");
        }

        AddDurableDependencyChecks(failures);
    }

    /// <summary>Adds the durable session, coordination and tenant-pepper requirements that apply outside Development.</summary>
    private void AddDurableDependencyChecks(List<string> failures)
    {
        if (!SessionsOptions.IsPostgresStore(_configuration[$"{SessionsOptions.SectionName}:Store"]))
        {
            failures.Add("Dispatch:Enabled requires Sessions:Store=Postgres outside Development.");
        }

        if (!HasSessionsConnectionString())
        {
            failures.Add("Dispatch:Enabled requires Sessions:Postgres:ConnectionString outside Development.");
        }

        if (IsInMemoryCoordination())
        {
            failures.Add("Dispatch:Enabled refuses Coordination:Store=InMemory outside Development.");
        }
        else if (!HasDragonflyConnectionString())
        {
            failures.Add("Dispatch:Enabled requires Coordination:Dragonfly:ConnectionString outside Development.");
        }

        if (string.IsNullOrWhiteSpace(_configuration[DataPlaneOptions.TenantHashPepperKey]))
        {
            failures.Add("DataPlane:TenantHashPepper is required outside Development.");
        }
    }

    /// <summary>Requires both signing keys to be P-256 private keys and to be distinct. Compares the private scalars in constant time, never the PEM text.</summary>
    private static void AddKeyChecks(DispatchOptions options, List<string> failures)
    {
        var credential = ReadP256PrivateScalar(options.Credentials.SigningKeyPem);
        var command = ReadP256PrivateScalar(options.CommandSigning.KeyPem);
        if (credential is null)
        {
            failures.Add("Dispatch:Credentials:SigningKeyPem must be a PKCS#8 PEM private key on the P-256 curve.");
        }

        if (command is null)
        {
            failures.Add("Dispatch:CommandSigning:KeyPem must be a PKCS#8 PEM private key on the P-256 curve.");
        }

        if (credential is not null && command is not null && CryptographicOperations.FixedTimeEquals(credential, command))
        {
            failures.Add("Dispatch:Credentials:SigningKeyPem and Dispatch:CommandSigning:KeyPem must be distinct keys.");
        }
    }

    private bool HasDurableCoordination()
    {
        var store = _configuration[$"{CoordinationOptions.SectionName}:Store"];
        var sessions = _configuration[$"{SessionsOptions.SectionName}:Store"];
        return CoordinationOptions.IsDragonflyStore(store)
            && HasDragonflyConnectionString()
            && SessionsOptions.IsPostgresStore(sessions)
            && HasSessionsConnectionString();
    }

    private bool HasSessionsConnectionString() => !string.IsNullOrWhiteSpace(_configuration[ServiceCollectionExtensions.ConnectionStringKey]);

    private bool HasDragonflyConnectionString() => !string.IsNullOrWhiteSpace(_configuration[CoordinationOptions.DragonflyConnectionStringKey]);

    private static bool IsHubOrigin(string value, bool requireHttps)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0)
        {
            return false;
        }

        return requireHttps
            ? string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            : string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
    }

    private bool IsInMemoryCoordination()
    {
        var store = _configuration[$"{CoordinationOptions.SectionName}:Store"];
        return string.IsNullOrWhiteSpace(store)
            || string.Equals(store, CoordinationOptions.InMemoryStore, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads the private scalar of a P-256 PKCS#8 PEM key, or returns null when the key is not one.</summary>
    /// <param name="pem">The PEM text.</param>
    /// <returns>The private scalar, or null when the PEM is blank, is not a private P-256 key, or does not parse.</returns>
    private static byte[]? ReadP256PrivateScalar(string pem)
    {
        if (string.IsNullOrWhiteSpace(pem))
        {
            return null;
        }

        using var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(CloudOptionsValidator.NormalizePem(pem));
            var scalar = key.ExportParameters(includePrivateParameters: true).D;
            return key.KeySize == 256 && scalar is { Length: > 0 } ? scalar : null;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return null;
        }
    }
}
