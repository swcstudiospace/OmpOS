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

        if (!development && !options.AllowAnyAuthenticatedUser && !options.AllowedSubjects.Any(static subject => !string.IsNullOrWhiteSpace(subject)))
        {
            failures.Add(
                "Api:Auth:AllowedSubjects must list at least one subject outside the Development environment, or Api:Auth:AllowAnyAuthenticatedUser must be true " +
                "(risky: the API runs an agent that can execute commands, so every account the identity provider admits could run it).");
        }
    }

    private static void ValidateOwnerToken(ApiAuthOptions options, List<string> failures)
    {
        if (!string.IsNullOrWhiteSpace(options.Authority) || !string.IsNullOrWhiteSpace(options.Audience) || !string.IsNullOrWhiteSpace(options.ValidIssuer))
        {
            failures.Add("Api:Auth:Authority, Api:Auth:Audience and Api:Auth:ValidIssuer must not be set when Api:Auth:Mode is OwnerToken.");
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

    private static bool IsLowercaseHexDigest(string? value) =>
        value is { Length: DigestLength } && value.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static bool IsAbsoluteHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
}
