using Microsoft.Extensions.Options;

namespace OmpOS.Api.Options;

/// <summary>
/// Validates <see cref="ApiAuthOptions"/> on start against the host environment. Outside Development a host needs an HTTPS
/// authority and an audience, and the development principal opt-in is refused. The environment comes from the host, not from
/// the configuration builder, so the rule applies to the environment the host actually runs under.
/// </summary>
internal sealed class ApiAuthOptionsValidator : IValidateOptions<ApiAuthOptions>
{
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

        if (options.AllowDevelopmentPrincipal && !development)
        {
            failures.Add("Api:Auth:AllowDevelopmentPrincipal is allowed only in the Development environment.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsAbsoluteHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
}
