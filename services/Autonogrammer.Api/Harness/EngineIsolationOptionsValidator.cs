using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// Validates the engine isolation keys of <see cref="HarnessOptions"/> on start, for the ACP adapter only. Outside Development the engine must not
/// inherit the API's environment unless <c>Harness:Acp:AllowInheritedEnvironment</c> says so, an allow-list entry must be a usable name or
/// prefix, <c>Harness:Acp:WorkspaceRoot</c> must be an absolute path, and the engine must have a working directory of its own: a run worktree
/// (<c>Worktrees:Root</c> and <c>Worktrees:Repository</c>) or <c>Harness:Acp:WorkspaceRoot</c>. Messages name keys, never values.
/// </summary>
internal sealed class EngineIsolationOptionsValidator : IValidateOptions<HarnessOptions>
{
    private readonly IHostEnvironment _environment;
    private readonly IOptions<WorktreesOptions> _worktrees;

    /// <summary>Initializes a new instance of the <see cref="EngineIsolationOptionsValidator"/> class.</summary>
    /// <param name="environment">The host environment.</param>
    /// <param name="worktrees">The worktree settings, which can give each run its own directory.</param>
    public EngineIsolationOptionsValidator(IHostEnvironment environment, IOptions<WorktreesOptions> worktrees)
    {
        _environment = environment;
        _worktrees = worktrees;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, HarnessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!HarnessOptions.IsAcpAdapter(options.Adapter))
        {
            return ValidateOptionsResult.Success;
        }

        var development = _environment.IsDevelopment();
        var settings = EngineIsolationSettings.Resolve(options.Acp, development);
        var failures = new List<string>();
        if (!development && settings.Mode == EngineEnvironmentMode.Inherit && !options.Acp.AllowInheritedEnvironment)
        {
            failures.Add(
                "Harness:Acp:Environment:Mode is Inherit outside the Development environment, which hands every secret of the API process to the engine and its tools. " +
                "Use Allowlist (the default), or set Harness:Acp:AllowInheritedEnvironment=true to accept the risk; that is unsafe.");
        }

        for (var index = 0; index < settings.Allow.Count; index++)
        {
            if (!EngineEnvironmentPolicy.IsValidAllowEntry(settings.Allow[index]))
            {
                failures.Add($"Harness:Acp:Environment:Allow:{index} must be a variable name, or a prefix ending in '*'; it must not be blank or contain '=', whitespace or a misplaced '*'.");
            }
        }

        if (settings.WorkspaceRoot is { } root && !Path.IsPathFullyQualified(root))
        {
            failures.Add("Harness:Acp:WorkspaceRoot must be an absolute path when set.");
        }

        if (!development && settings.WorkspaceRoot is null && !HasWorktrees(_worktrees.Value))
        {
            failures.Add("Harness:Acp:WorkspaceRoot is required outside the Development environment when Worktrees:Root and Worktrees:Repository are not both set; without it the engine would start in the API's own working directory.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool HasWorktrees(WorktreesOptions options) =>
        !string.IsNullOrWhiteSpace(options.Root) && !string.IsNullOrWhiteSpace(options.Repository);
}
