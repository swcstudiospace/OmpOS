using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork;

/// <summary>Validates <see cref="CoworkOptions"/> on start. Every failure names the key and never repeats a value.</summary>
internal sealed class CoworkOptionsValidator : IValidateOptions<CoworkOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, CoworkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.MaxSessionsPerOwner is < 1 or > CoworkOptions.DefaultMaxSessionsPerOwner)
        {
            failures.Add("Cowork:MaxSessionsPerOwner must be between 1 and 200.");
        }

        if (options.MaxTitleLength is < 1 or > CoworkOptions.DefaultMaxTitleLength)
        {
            failures.Add("Cowork:MaxTitleLength must be between 1 and 200.");
        }

        if (options.RequestsPerMinute is < 1 or > 100_000)
        {
            failures.Add("Cowork:RequestsPerMinute must be between 1 and 100000.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
