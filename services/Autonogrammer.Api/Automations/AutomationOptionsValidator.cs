using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Automations;

/// <summary>
/// Validates <see cref="AutomationOptions"/> on start. Nothing is checked while automations are off. Every failure names the key and never
/// repeats a value.
/// </summary>
internal sealed class AutomationOptionsValidator : IValidateOptions<AutomationOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AutomationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (options.StorePath.Length > 0 && !Path.IsPathRooted(options.StorePath))
        {
            failures.Add("Automations:StorePath must be an absolute path when set.");
        }

        if (options.SecretStorePath.Length > 0 && !Path.IsPathRooted(options.SecretStorePath))
        {
            failures.Add("Automations:SecretStorePath must be an absolute path when set.");
        }

        if (options.WebhookRequestsPerMinute is < 1 or > 100_000)
        {
            failures.Add("Automations:WebhookRequestsPerMinute must be between 1 and 100000.");
        }

        if (options.RequestsPerMinute is < 1 or > 100_000)
        {
            failures.Add("Automations:RequestsPerMinute must be between 1 and 100000.");
        }

        if (options.WebhookReplayWindowSeconds is < 30 or > 3600)
        {
            failures.Add("Automations:WebhookReplayWindowSeconds must be between 30 and 3600.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
