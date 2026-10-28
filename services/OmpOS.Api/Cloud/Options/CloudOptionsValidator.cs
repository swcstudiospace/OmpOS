using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace OmpOS.Api.Cloud.Options;

/// <summary>
/// Validates <see cref="CloudOptions"/> on start. Nothing is checked while cloud is off, so a default host never trips on it. When cloud is on,
/// every failure names the configuration key and never repeats a value: the section holds keys and tokens.
/// </summary>
internal sealed class CloudOptionsValidator : IValidateOptions<CloudOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, CloudOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        ValidateEnvironment(options.Environment, failures);
        ValidateLimits(options, failures);
        ValidateSweeper(options.Sweeper, failures);
        ValidateWorker(options.Worker, failures);
        ValidateGit(options.Git, failures);
        ValidateSecrets(options.Secrets, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateEnvironment(CloudOptions.EnvironmentSettings environment, List<string> failures)
    {
        var provider = environment.Provider;
        if (string.Equals(provider, CloudOptions.EnvironmentSettings.RailwayProvider, StringComparison.OrdinalIgnoreCase))
        {
            Require(environment.Railway.ApiToken, "Cloud:Environment:Railway:ApiToken", failures);
            Require(environment.Railway.ProjectId, "Cloud:Environment:Railway:ProjectId", failures);
            Require(environment.Railway.EnvironmentId, "Cloud:Environment:Railway:EnvironmentId", failures);
            Require(environment.Railway.Image, "Cloud:Environment:Railway:Image", failures);
            if (environment.Railway.WorkerPublicKeysPem.Count == 0)
            {
                failures.Add("Cloud:Environment:Railway:WorkerPublicKeysPem needs at least one public key: a worker with no key refuses every connection.");
            }

            if (!string.IsNullOrWhiteSpace(environment.Railway.Endpoint)
                && !(Uri.TryCreate(environment.Railway.Endpoint, UriKind.Absolute, out var endpoint) && string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
            {
                failures.Add("Cloud:Environment:Railway:Endpoint must be an absolute https URI when set.");
            }
        }
        else if (string.Equals(provider, CloudOptions.EnvironmentSettings.StaticPoolProvider, StringComparison.OrdinalIgnoreCase))
        {
            if (environment.StaticPool.Workers.Count == 0)
            {
                failures.Add("Cloud:Environment:StaticPool:Workers needs at least one worker.");
            }

            for (var index = 0; index < environment.StaticPool.Workers.Count; index++)
            {
                var worker = environment.StaticPool.Workers[index];
                var prefix = string.Create(CultureInfo.InvariantCulture, $"Cloud:Environment:StaticPool:Workers:{index}");
                Require(worker.WorkerId, prefix + ":WorkerId", failures);
                if (!(Uri.TryCreate(worker.BaseUri, UriKind.Absolute, out var baseUri) && IsHttp(baseUri)))
                {
                    failures.Add(prefix + ":BaseUri must be an absolute http or https URI.");
                }
            }
        }
        else if (!string.Equals(provider, CloudOptions.EnvironmentSettings.ExternalProvider, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Cloud:Environment:Provider must be Railway, StaticPool or External when Cloud:Enabled is true; no environment client is configured.");
        }

        if (!environment.VolumeMountPath.StartsWith('/'))
        {
            failures.Add("Cloud:Environment:VolumeMountPath must be an absolute path.");
        }
    }

    private static void ValidateLimits(CloudOptions options, List<string> failures)
    {
        var limits = options.Limits;
        if (limits.MaxAgentsPerOwner < 1)
        {
            failures.Add("Cloud:Limits:MaxAgentsPerOwner must be at least 1.");
        }

        if (limits.MaxConcurrentRunningPerOwner < 1 || limits.MaxConcurrentRunningPerOwner > limits.MaxAgentsPerOwner)
        {
            failures.Add("Cloud:Limits:MaxConcurrentRunningPerOwner must be between 1 and Cloud:Limits:MaxAgentsPerOwner.");
        }

        if (limits.MaxIdleMinutes < 1)
        {
            failures.Add("Cloud:Limits:MaxIdleMinutes must be at least 1.");
        }

        if (limits.DefaultIdleMinutes < 1 || limits.DefaultIdleMinutes > limits.MaxIdleMinutes)
        {
            failures.Add("Cloud:Limits:DefaultIdleMinutes must be between 1 and Cloud:Limits:MaxIdleMinutes.");
        }

        if (limits.MaxComputeSecondsPerAgent < 0)
        {
            failures.Add("Cloud:Limits:MaxComputeSecondsPerAgent must be 0 (no limit) or positive.");
        }

        if (limits.MaxSecretsPerOwner < 1)
        {
            failures.Add("Cloud:Limits:MaxSecretsPerOwner must be at least 1.");
        }
    }

    private static void ValidateSweeper(CloudOptions.SweeperSettings sweeper, List<string> failures)
    {
        if (sweeper.Interval <= TimeSpan.Zero)
        {
            failures.Add("Cloud:Sweeper:Interval must be positive.");
        }

        if (sweeper.OrphanScanInterval <= TimeSpan.Zero)
        {
            failures.Add("Cloud:Sweeper:OrphanScanInterval must be positive.");
        }

        if (sweeper.ProvisioningTimeout <= TimeSpan.Zero)
        {
            failures.Add("Cloud:Sweeper:ProvisioningTimeout must be positive.");
        }
    }

    private static void ValidateWorker(CloudOptions.WorkerSettings worker, List<string> failures)
    {
        if (worker.TokenLifetime <= TimeSpan.Zero || worker.TokenLifetime > CloudOptions.WorkerSettings.MaximumTokenLifetime)
        {
            failures.Add("Cloud:Worker:TokenLifetime must be positive and at most 15 minutes: workers refuse a longer token.");
        }

        Require(worker.Issuer, "Cloud:Worker:Issuer", failures);
        if (!worker.WorkspaceRoot.StartsWith('/'))
        {
            failures.Add("Cloud:Worker:WorkspaceRoot must be an absolute path on the worker.");
        }

        if (string.IsNullOrWhiteSpace(worker.SigningKeyPem))
        {
            failures.Add("Cloud:Worker:SigningKeyPem is required when Cloud:Enabled is true.");
        }
        else if (!IsEcP256PrivateKey(worker.SigningKeyPem))
        {
            failures.Add("Cloud:Worker:SigningKeyPem must be a PKCS#8 PEM private key on the P-256 curve (ES256).");
        }
    }

    private static void ValidateGit(CloudOptions.GitSettings git, List<string> failures)
    {
        if (!(Uri.TryCreate(git.ApiBase, UriKind.Absolute, out var apiBase) && string.Equals(apiBase.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            failures.Add("Cloud:Git:ApiBase must be an absolute https URI.");
        }

        if (git.AllowedHosts.Count == 0 || git.AllowedHosts.Exists(static host => string.IsNullOrWhiteSpace(host)))
        {
            failures.Add("Cloud:Git:AllowedHosts must list at least one host and no blank entry.");
        }

        var appId = !string.IsNullOrWhiteSpace(git.App.AppId);
        var appKey = !string.IsNullOrWhiteSpace(git.App.PrivateKeyPem);
        if (appId != appKey)
        {
            failures.Add("Cloud:Git:App:AppId and Cloud:Git:App:PrivateKeyPem must be set together.");
        }
        else if (appKey && !IsRsaPrivateKey(git.App.PrivateKeyPem))
        {
            failures.Add("Cloud:Git:App:PrivateKeyPem must be an RSA private key PEM.");
        }
    }

    private static void ValidateSecrets(CloudOptions.SecretSettings secrets, List<string> failures)
    {
        foreach (var (secretName, value) in secrets.Static)
        {
            if (!CloudSecretNames.IsValid(secretName))
            {
                failures.Add("Cloud:Secrets:Static holds a name that is not a valid secret name (letters, digits and underscores, not reserved, at most 64 characters).");
            }
            else if (value.Length < CloudSecretNames.MinimumValueLength)
            {
                failures.Add($"Cloud:Secrets:Static:{secretName} is shorter than {CloudSecretNames.MinimumValueLength.ToString(CultureInfo.InvariantCulture)} characters and could not be redacted reliably.");
            }
        }
    }

    private static void Require(string? value, string key, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add(key + " is required when Cloud:Enabled is true.");
        }
    }

    private static bool IsHttp(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);

    private static bool IsEcP256PrivateKey(string pem)
    {
        using var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(NormalizePem(pem));
            return key.KeySize == 256 && key.ExportParameters(includePrivateParameters: true).D is { Length: > 0 };
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static bool IsRsaPrivateKey(string pem)
    {
        using var key = RSA.Create();
        try
        {
            key.ImportFromPem(NormalizePem(pem));
            return key.KeySize >= 2048 && key.ExportParameters(includePrivateParameters: true).D is { Length: > 0 };
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Accepts a PEM whose line breaks were written as the two characters <c>\n</c>, as many secret stores and environment files do.</summary>
    /// <param name="pem">The configured PEM text.</param>
    /// <returns>The PEM with real line breaks.</returns>
    internal static string NormalizePem(string pem) =>
        pem.Contains('\n', StringComparison.Ordinal) ? pem : pem.Replace("\\n", "\n", StringComparison.Ordinal);
}
