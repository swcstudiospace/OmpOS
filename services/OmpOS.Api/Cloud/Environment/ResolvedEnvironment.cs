namespace OmpOS.Api.Cloud;

/// <summary>The environment configuration an agent will run with, and why that one.</summary>
/// <param name="Config">The chosen configuration, or null when none applies.</param>
/// <param name="Errors">Errors of a file that was chosen but invalid. Non-empty means the agent must not start with a guess.</param>
/// <param name="Warnings">Things ignored or not supported (for example a Dockerfile build).</param>
internal sealed record ResolvedEnvironment(EnvironmentConfig? Config, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
