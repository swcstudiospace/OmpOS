using Microsoft.Extensions.Options;

namespace Autonogrammer.Harness.BrowserUse.Options;

/// <summary>
/// Validates <see cref="BrowserUseOptions"/> on start. Every failure names a <c>Cowork:BrowserUse</c> key and never repeats a value.
/// </summary>
public sealed class BrowserUseOptionsValidator : IValidateOptions<BrowserUseOptions>
{
    /// <summary>The environment variable that permits <see cref="SandboxMode.Disabled"/> on a root test host.</summary>
    public const string AllowNoSandboxVariable = "AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX";

    private readonly Func<string, string?> _environment;

    /// <summary>Initializes a validator that reads the process environment.</summary>
    public BrowserUseOptionsValidator()
        : this(static name => Environment.GetEnvironmentVariable(name))
    {
    }

    /// <summary>Initializes a validator with an injected environment reader.</summary>
    /// <param name="environment">Returns the value of a variable, or null when unset.</param>
    public BrowserUseOptionsValidator(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BrowserUseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        ValidateEnums(options, failures);
        ValidateCounts(options, failures);
        ValidateDurations(options, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>Returns the configuration key for a property. The key never uses a forbidden section prefix.</summary>
    /// <param name="propertyName">The options property name.</param>
    /// <returns>The <c>Cowork:BrowserUse</c> key.</returns>
    public static string Key(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return BrowserUseOptions.SectionName + ":" + propertyName;
    }

    private void ValidateEnums(BrowserUseOptions options, List<string> failures)
    {
        RequireDefined(options.Source, Key(nameof(BrowserUseOptions.Source)), failures);
        RequireDefined(options.PolicyMode, Key(nameof(BrowserUseOptions.PolicyMode)), failures);
        RequireDefined(options.DownloadMode, Key(nameof(BrowserUseOptions.DownloadMode)), failures);
        if (options.DownloadMode is not DownloadMode.Deny and not DownloadMode.Quarantine)
        {
            failures.Add(Key(nameof(BrowserUseOptions.DownloadMode)) + " accepts only Deny or Quarantine.");
        }

        RequireDefined(options.SandboxMode, Key(nameof(BrowserUseOptions.SandboxMode)), failures);
        if (options.SandboxMode == SandboxMode.Disabled
            && !string.Equals(_environment(AllowNoSandboxVariable), "1", StringComparison.Ordinal))
        {
            failures.Add(Key(nameof(BrowserUseOptions.SandboxMode)) + " is refused unless " + AllowNoSandboxVariable + " is 1.");
        }
    }

    private static void ValidateCounts(BrowserUseOptions options, List<string> failures)
    {
        if (options.MinimumMajorVersion < 1)
        {
            failures.Add(Key(nameof(BrowserUseOptions.MinimumMajorVersion)) + " must be a positive major version.");
        }

        if (options.ViewportWidth < 1)
        {
            failures.Add(Key(nameof(BrowserUseOptions.ViewportWidth)) + " must be a positive pixel count.");
        }

        if (options.ViewportHeight < 1)
        {
            failures.Add(Key(nameof(BrowserUseOptions.ViewportHeight)) + " must be a positive pixel count.");
        }

        RequirePositive(options.MaxConcurrentBrowsers, nameof(BrowserUseOptions.MaxConcurrentBrowsers), failures);
        RequirePositive(options.MaxTabs, nameof(BrowserUseOptions.MaxTabs), failures);
        RequirePositive(options.MaxActionsPerMinute, nameof(BrowserUseOptions.MaxActionsPerMinute), failures);
        RequirePositive(options.MaxCallsPerSession, nameof(BrowserUseOptions.MaxCallsPerSession), failures);
        RequirePositive(options.MaxScreenshotsPerSession, nameof(BrowserUseOptions.MaxScreenshotsPerSession), failures);
        RequirePositive(options.MaxReadChars, nameof(BrowserUseOptions.MaxReadChars), failures);
        RequirePositive(options.MaxBrowserMemoryMiB, nameof(BrowserUseOptions.MaxBrowserMemoryMiB), failures);
        RequirePositive(options.LocalFramesPerSecond, nameof(BrowserUseOptions.LocalFramesPerSecond), failures);
        if (options.MaxScreenshotBytes < 1 || options.MaxScreenshotBytes > BrowserUseOptions.MaxScreenshotBytesCeiling)
        {
            failures.Add(Key(nameof(BrowserUseOptions.MaxScreenshotBytes)) + " must be a positive size at or under the host cap.");
        }

        if (options.ScreenshotLongEdge < 640 || options.ScreenshotLongEdge > 2576)
        {
            failures.Add(Key(nameof(BrowserUseOptions.ScreenshotLongEdge)) + " must be between the documented bounds.");
        }
    }

    private static void ValidateDurations(BrowserUseOptions options, List<string> failures)
    {
        if (options.IdleTimeout <= TimeSpan.Zero)
        {
            failures.Add(Key(nameof(BrowserUseOptions.IdleTimeout)) + " must be a positive duration.");
        }

        if (options.MaxSessionLifetime <= TimeSpan.Zero)
        {
            failures.Add(Key(nameof(BrowserUseOptions.MaxSessionLifetime)) + " must be a positive duration.");
        }
    }

    private static void RequirePositive(int value, string propertyName, List<string> failures)
    {
        if (value < 1)
        {
            failures.Add(Key(propertyName) + " must be a positive count.");
        }
    }

    private static void RequireDefined<TEnum>(TEnum value, string key, List<string> failures)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            failures.Add(key + " is not a defined value.");
        }
    }
}
