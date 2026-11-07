using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Options;

/// <summary>
/// Validates <see cref="ApiAuthOptions"/> on start against the host environment. Outside Development a host needs an HTTPS
/// authority and an audience, and the development principal opt-in is refused. The environment comes from the host, not from
/// the configuration builder, so the rule applies to the environment the host actually runs under.
/// </summary>
internal sealed class ApiAuthOptionsValidator : IValidateOptions<ApiAuthOptions>
{
    private const int MaxDigests = 3;
    private const int DigestLength = 64;
    private const int MaxFailuresLimit = 1000;
    private const int DefaultOtpExpirySeconds = 3600;
    private const int DefaultResendCooldownSeconds = 60;

    private readonly IHostEnvironment _environment;

    /// <summary>Initializes a new instance of the <see cref="ApiAuthOptionsValidator"/> class.</summary>
    /// <param name="environment">The host environment.</param>
    public ApiAuthOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ApiAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var development = _environment.IsDevelopment();
        var failures = new List<string>();
        if (options.Mode == ApiAuthMode.OwnerToken)
        {
            ValidateOwnerToken(options, failures);
        }
        else
        {
            ValidateOidc(options, development, failures);
        }

        if (options.AllowDevelopmentPrincipal && !development)
        {
            failures.Add("Api:Auth:AllowDevelopmentPrincipal is allowed only in the Development environment.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateOidc(ApiAuthOptions options, bool development, List<string> failures)
    {
        if (!development && string.IsNullOrWhiteSpace(options.Authority))
        {
            failures.Add("Api:Auth:Authority is required outside the Development environment.");
        }

        if (!development && string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Api:Auth:Audience is required outside the Development environment.");
        }

        if (!string.IsNullOrWhiteSpace(options.Authority) && !IsAbsoluteHttps(options.Authority))
        {
            failures.Add("Api:Auth:Authority must be an absolute https URI.");
        }

        if (!development && string.IsNullOrWhiteSpace(options.ValidIssuer))
        {
            failures.Add("Api:Auth:ValidIssuer is required outside the Development environment.");
        }

        if (!string.IsNullOrWhiteSpace(options.ValidIssuer) && !IsAbsoluteHttps(options.ValidIssuer))
        {
            failures.Add("Api:Auth:ValidIssuer must be an absolute https URI.");
        }

        if (options.Algorithms.Any(static a => !string.IsNullOrWhiteSpace(a) && a is not ("ES256" or "RS256")))
        {
            failures.Add("Api:Auth:Algorithms may list only ES256 and RS256.");
        }

        if (!development && !options.AllowAnyAuthenticatedUser && !options.AllowedSubjects.Any(static subject => !string.IsNullOrWhiteSpace(subject)))
        {
            failures.Add(
                "Api:Auth:AllowedSubjects must list at least one subject outside the Development environment, or Api:Auth:AllowAnyAuthenticatedUser must be true " +
                "(risky: the API runs an agent that can execute commands, so every account the identity provider admits could run it).");
        }

        ValidateClient(options.Client, failures);
    }

    private static void ValidateOwnerToken(ApiAuthOptions options, List<string> failures)
    {
        if (!string.IsNullOrWhiteSpace(options.Authority) || !string.IsNullOrWhiteSpace(options.Audience) || !string.IsNullOrWhiteSpace(options.ValidIssuer))
        {
            failures.Add("Api:Auth:Authority, Api:Auth:Audience and Api:Auth:ValidIssuer must not be set when Api:Auth:Mode is OwnerToken.");
        }

        if (options.Algorithms.Any(static a => !string.IsNullOrWhiteSpace(a)))
        {
            failures.Add("Api:Auth:Algorithms must not be set when Api:Auth:Mode is OwnerToken.");
        }

        if (ClientIsConfigured(options.Client))
        {
            failures.Add("Api:Auth:Client must not be set when Api:Auth:Mode is OwnerToken.");
        }

        var digests = options.OwnerToken.Sha256;
        if (digests.Count is < 1 or > MaxDigests)
        {
            failures.Add($"Api:Auth:OwnerToken:Sha256 must list between 1 and {MaxDigests} digests.");
        }
        else if (digests.Any(static digest => !IsLowercaseHexDigest(digest)))
        {
            failures.Add("Api:Auth:OwnerToken:Sha256 entries must be 64 lowercase hexadecimal characters (a SHA-256 digest).");
        }

        if (string.IsNullOrWhiteSpace(options.OwnerToken.Subject))
        {
            failures.Add("Api:Auth:OwnerToken:Subject must not be empty.");
        }

        if (options.OwnerToken.MaxFailuresPerMinute is < 1 or > MaxFailuresLimit)
        {
            failures.Add($"Api:Auth:OwnerToken:MaxFailuresPerMinute must be between 1 and {MaxFailuresLimit}.");
        }
    }

    private static void ValidateClient(ApiAuthClientOptions client, List<string> failures)
    {
        var key = client.PublishableKey?.Trim();
        if (!string.IsNullOrEmpty(key))
        {
            if (key.StartsWith("sb_secret_", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("Api:Auth:Client:PublishableKey must be a publishable key, not a secret key.");
            }
            else if (IsServiceRoleJwt(key))
            {
                failures.Add("Api:Auth:Client:PublishableKey must be a publishable key, not a service-role token.");
            }
        }

        if (!string.IsNullOrWhiteSpace(client.ProjectUrl)
            && (!Uri.TryCreate(client.ProjectUrl, UriKind.Absolute, out var url)
                || !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                || url.UserInfo.Length > 0))
        {
            failures.Add("Api:Auth:Client:ProjectUrl must be an absolute https URI without user information.");
        }

        if (client.MagicLinkEnabled && !client.EmailCodeEnabled)
        {
            failures.Add("Api:Auth:Client:MagicLinkEnabled requires Api:Auth:Client:EmailCodeEnabled.");
        }

        if (client.EmailOtpExpirySeconds < 1)
        {
            failures.Add("Api:Auth:Client:EmailOtpExpirySeconds must be at least 1.");
        }

        if (client.ResendCooldownSeconds < 0)
        {
            failures.Add("Api:Auth:Client:ResendCooldownSeconds must not be negative.");
        }

        if (client.Providers.Any(static provider => provider is not ("google" or "x")))
        {
            failures.Add("Api:Auth:Client:Providers may list only google and x.");
        }

        if (client.Providers.Distinct(StringComparer.Ordinal).Count() != client.Providers.Count)
        {
            failures.Add("Api:Auth:Client:Providers must not list a provider twice.");
        }

        if ((client.Providers.Count > 0 || client.WalletEnabled)
            && (string.IsNullOrWhiteSpace(client.ProjectUrl) || string.IsNullOrWhiteSpace(client.PublishableKey)))
        {
            failures.Add("Api:Auth:Client:Providers and Api:Auth:Client:WalletEnabled require Api:Auth:Client:ProjectUrl and Api:Auth:Client:PublishableKey.");
        }
    }

    private static bool ClientIsConfigured(ApiAuthClientOptions client) =>
        client.HasAny
        || client.MagicLinkEnabled
        || !client.EmailCodeEnabled
        || client.SignupEmailHasCode
        || client.EmailOtpExpirySeconds != DefaultOtpExpirySeconds
        || client.ResendCooldownSeconds != DefaultResendCooldownSeconds
        || client.Providers.Count > 0
        || client.WalletEnabled;

    /// <summary>Returns whether a key is a JWT whose payload carries <c>role</c> equal to <c>service_role</c>; a key that is not a readable JWT is not.</summary>
    private static bool IsServiceRoleJwt(string key)
    {
        var parts = key.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var document = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty("role", out var role)
                && role.ValueKind == System.Text.Json.JsonValueKind.String
                && string.Equals(role.GetString(), "service_role", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool IsLowercaseHexDigest(string? value) =>
        value is { Length: DigestLength } && value.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static bool IsAbsoluteHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
}
