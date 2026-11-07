using Microsoft.Extensions.Options;

namespace Autonogrammer.Cowork.Artifacts;

/// <summary>
/// Validates <see cref="ArtifactOptions"/>. Every failure names the key and never repeats a value.
/// </summary>
public sealed class ArtifactOptionsValidator : IValidateOptions<ArtifactOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ArtifactOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        RequirePositive(failures, "MaxTextBytes", options.MaxTextBytes);
        RequirePositive(failures, "MaxSvgBytes", options.MaxSvgBytes);
        RequirePositive(failures, "MaxMermaidBytes", options.MaxMermaidBytes);
        RequirePositive(failures, "MaxImageBytes", options.MaxImageBytes);
        RequirePositive(failures, "MaxVersions", options.MaxVersions);
        RequirePositive(failures, "MaxArtifacts", options.MaxArtifacts);
        RequirePositive(failures, "MaxAccountBytes", options.MaxAccountBytes);
        RequirePositive(failures, "MaxWritesPerMinutePerSession", options.MaxWritesPerMinutePerSession);
        if (string.IsNullOrWhiteSpace(options.Surface) || options.Surface.Length > 32 || options.Surface.Any(char.IsControl))
        {
            failures.Add(Key("Surface") + " is invalid.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void RequirePositive(List<string> failures, string property, long value)
    {
        if (value <= 0)
        {
            failures.Add(Key(property) + " is invalid.");
        }
    }

    private static string Key(string property) => ArtifactOptions.SectionName + ":" + property;
}
