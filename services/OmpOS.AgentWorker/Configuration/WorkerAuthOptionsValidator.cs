using Microsoft.Extensions.Options;
using OmpOS.AgentWorker.Security;

namespace OmpOS.AgentWorker.Configuration;

/// <summary>
/// Validates <see cref="WorkerAuthOptions"/> on start. The worker refuses to start without a usable public key, so a misconfigured
/// worker fails at deploy time instead of rejecting every connection. Failure messages name the key index and never the key value.
/// </summary>
internal sealed class WorkerAuthOptionsValidator : IValidateOptions<WorkerAuthOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, WorkerAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Worker:Auth:Issuer must not be blank.");
        }

        if (options.MaxTokenLifetime <= TimeSpan.Zero || options.MaxTokenLifetime > WorkerAuthOptions.MaximumTokenLifetime)
        {
            failures.Add("Worker:Auth:MaxTokenLifetime must be greater than zero and at most 00:15:00.");
        }

        if (options.ClockSkew < TimeSpan.Zero || options.ClockSkew > WorkerAuthOptions.MaximumClockSkew)
        {
            failures.Add("Worker:Auth:ClockSkew must be between zero and 00:02:00.");
        }

        try
        {
            using var keys = WorkerVerificationKeySet.Parse(options.PublicKeysPem);
        }
        catch (FormatException ex)
        {
            failures.Add("Worker:Auth:PublicKeysPem is invalid: " + ex.Message);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
