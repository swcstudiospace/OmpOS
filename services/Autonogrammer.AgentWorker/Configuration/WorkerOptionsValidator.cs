using Autonogrammer.AgentWorker.Security;
using Microsoft.Extensions.Options;

namespace Autonogrammer.AgentWorker.Configuration;

/// <summary>Validates <see cref="WorkerOptions"/> on start so a bad path, interval or session pin stops the worker before it accepts a connection.</summary>
internal sealed class WorkerOptionsValidator : IValidateOptions<WorkerOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, WorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.WorkspaceRoot) || !Path.IsPathFullyQualified(options.WorkspaceRoot))
        {
            failures.Add("Worker:WorkspaceRoot must be an absolute path.");
        }

        if (options.PingInterval <= TimeSpan.Zero || options.PingInterval > WorkerOptions.MaximumPingInterval)
        {
            failures.Add("Worker:PingInterval must be greater than zero and at most 00:05:00.");
        }

        if (options.CloseTimeout <= TimeSpan.Zero)
        {
            failures.Add("Worker:CloseTimeout must be greater than zero.");
        }

        if (options.SessionId is not null && !WorkerSessionIds.IsSafe(options.SessionId))
        {
            failures.Add("Worker:SessionId must be 1 to 64 letters, digits, '.', '_' or '-', starting with a letter or digit.");
        }

        if (options.Omp is null || string.IsNullOrWhiteSpace(options.Omp.Path))
        {
            failures.Add("Worker:Omp:Path must not be blank.");
        }
        else if (options.Omp.ExtraArguments.Any(static argument => string.IsNullOrEmpty(argument) || argument.Contains('\0', StringComparison.Ordinal)))
        {
            failures.Add("Worker:Omp:ExtraArguments must not contain blank entries or NUL.");
        }

        ValidateHooks(options.Hooks, failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateHooks(WorkerHooksOptions? hooks, List<string> failures)
    {
        if (hooks is null)
        {
            failures.Add("Worker:Hooks must not be null.");
            return;
        }

        if (hooks.MaxSnapshotHashes is < 1 or > 4096)
        {
            failures.Add("Worker:Hooks:MaxSnapshotHashes must be between 1 and 4096.");
        }

        if (!string.IsNullOrWhiteSpace(hooks.StateRoot) && !Path.IsPathFullyQualified(hooks.StateRoot))
        {
            failures.Add("Worker:Hooks:StateRoot must be an absolute path.");
        }

        if (hooks.Enabled && (string.IsNullOrWhiteSpace(hooks.PluginsRoot) || !Path.IsPathFullyQualified(hooks.PluginsRoot)))
        {
            failures.Add("Worker:Hooks:PluginsRoot must be an absolute path when Worker:Hooks:Enabled is true.");
        }
    }
}
