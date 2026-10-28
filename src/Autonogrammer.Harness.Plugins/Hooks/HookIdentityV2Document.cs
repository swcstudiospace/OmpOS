namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The canonical form a version 2 consent hash is computed over.
/// </summary>
/// <remarks>
/// Field order is part of the hash, so reordering, renaming or adding a field changes every digest and voids existing
/// consent. The hashed fields are, in order: HashVersion, Event, Omp, Scope, Type, Command, Args, Shell, Cwd, Env (sorted by
/// name), Prompt, Model, FailClosed, Matcher, TimeoutSeconds, LoopLimit, RunsIn, Executables (sorted by path).
/// </remarks>
internal sealed class HookIdentityV2Document
{
    public int HashVersion { get; set; }

    public string Event { get; set; } = string.Empty;

    public bool Omp { get; set; }

    public string Scope { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Command { get; set; }

    public List<string>? Args { get; set; }

    public string Shell { get; set; } = string.Empty;

    public string Cwd { get; set; } = string.Empty;

    public List<string> Env { get; set; } = [];

    public string? Prompt { get; set; }

    public string? Model { get; set; }

    public bool FailClosed { get; set; }

    public string? Matcher { get; set; }

    public double TimeoutSeconds { get; set; }

    public int? LoopLimit { get; set; }

    public int RunsIn { get; set; }

    public List<string> Executables { get; set; } = [];
}
