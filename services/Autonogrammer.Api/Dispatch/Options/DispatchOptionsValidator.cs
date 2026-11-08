using System.Security.Cryptography;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Api.Options;
using Autonogrammer.Sessions.Postgres;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Validates <see cref="DispatchOptions"/> on start. Nothing else is checked while dispatch is off, so design-time OpenAPI generation
/// with the defaults succeeds. Failures name keys and never a secret.
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

        if (!Enum.IsDefined(options.ReplicaMode))
        {
            failures.Add("Dispatch:ReplicaMode must be Single or Multi.");
        }
        else if (options.ReplicaMode == DispatchOptions.DispatchReplicaMode.Multi && !HasDurableCoordination())
        {
            failures.Add("Dispatch:ReplicaMode=Multi requires Coordination:Store=Dragonfly with Coordination:Dragonfly:ConnectionString and Sessions:Store=Postgres with Sessions:Postgres:ConnectionString.");
        }

        if (!_environment.IsDevelopment() && IsInMemoryCoordination())
        {
            failures.Add("Dispatch:Enabled refuses Coordination:Store=InMemory outside Development.");
        }

        if (!_environment.IsDevelopment() && !string.Equals(_configuration["Sessions:Store"], "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Dispatch:Enabled requires Sessions:Store=Postgres outside Development.");
        }

        if (!_environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.HubOrigin))
        {
            failures.Add("Dispatch:HubOrigin is required outside Development. The origin comes from configuration, never from a request header.");
        }

        if (!string.IsNullOrWhiteSpace(options.HubOrigin) && !IsHubOrigin(options.HubOrigin, !_environment.IsDevelopment()))
        {
            failures.Add("Dispatch:HubOrigin must be an absolute origin with a scheme and host and no path; https outside Development.");
        }

        RequirePrivateKey(options.Credentials.SigningKeyPem, "Dispatch:Credentials:SigningKeyPem", failures);
        RequirePrivateKey(options.CommandSigning.KeyPem, "Dispatch:CommandSigning:KeyPem", failures);
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

    private bool HasDurableCoordination()
    {
        var store = _configuration[$"{CoordinationOptions.SectionName}:Store"];
        var dragonfly = _configuration[CoordinationOptions.DragonflyConnectionStringKey];
        var sessions = _configuration["Sessions:Store"];
        var postgres = _configuration[ServiceCollectionExtensions.ConnectionStringKey];
        return CoordinationOptions.IsDragonflyStore(store)
            && !string.IsNullOrWhiteSpace(dragonfly)
            && string.Equals(sessions, "Postgres", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(postgres);
    }

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

    private static void RequirePrivateKey(string pem, string key, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(pem) || !IsP256PrivateKey(pem))
        {
            failures.Add(key + " must be a PKCS#8 PEM private key on the P-256 curve.");
        }
    }

    private static bool IsP256PrivateKey(string pem)
    {
        using var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(CloudOptionsValidator.NormalizePem(pem));
            return key.KeySize == 256 && key.ExportParameters(includePrivateParameters: true).D is { Length: > 0 };
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return false;
        }
    }
}
