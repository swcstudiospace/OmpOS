using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Hosting;

/// <summary>
/// Validates the nested sections of <see cref="HostingOptions"/> and the cross-field rules: a trusted-network list is required
/// for <see cref="ForwardedHeadersMode.KnownNetworks"/> and every entry must be a CIDR range.
/// </summary>
internal sealed class HostingOptionsValidator : IValidateOptions<HostingOptions>
{
    /// <summary>Returns whether <paramref name="options"/> passes every rule.</summary>
    /// <param name="options">The bound options.</param>
    /// <returns>True when the options are valid.</returns>
    public static bool IsValid(HostingOptions options) => Collect(options).Count == 0;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, HostingOptions options)
    {
        var failures = Collect(options);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static List<string> Collect(HostingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        AddFailures(options.ForwardedHeaders, failures);
        AddFailures(options.Hsts, failures);
        AddFailures(options.SecurityHeaders, failures);
        AddFailures(options.Kestrel, failures);
        AddFailures(options.Sse, failures);

        var forwarded = options.ForwardedHeaders;
        if (forwarded.Mode == ForwardedHeadersMode.KnownNetworks && forwarded.KnownNetworks.Count == 0)
        {
            failures.Add("Hosting:ForwardedHeaders:KnownNetworks must list at least one CIDR range when Mode is KnownNetworks.");
        }

        foreach (var network in forwarded.KnownNetworks)
        {
            if (!System.Net.IPNetwork.TryParse(network, out _))
            {
                failures.Add("Hosting:ForwardedHeaders:KnownNetworks contains an entry that is not a CIDR range.");
                break;
            }
        }

        return failures;
    }

    private static void AddFailures(object section, List<string> failures)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(section, new ValidationContext(section), results, validateAllProperties: true))
        {
            failures.AddRange(results.Select(static result => result.ErrorMessage ?? "Hosting options are invalid."));
        }
    }
}
