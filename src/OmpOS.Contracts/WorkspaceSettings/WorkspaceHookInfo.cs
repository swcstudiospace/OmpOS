using System.Text.Json.Serialization;

namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>One hook of the effective hook set. Nothing here starts the hook.</summary>
public sealed record WorkspaceHookInfo
{
    /// <summary>Gets the hook identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the id the Hooks page groups the hook under.</summary>
    public required string PluginId { get; init; }

    /// <summary>Gets the scope of the file the hook came from, for example <c>project</c> or <c>user</c>.</summary>
    public required string Scope { get; init; }

    /// <summary>Gets the event name the hook runs on.</summary>
    public required string EventName { get; init; }

    /// <summary>Gets a value indicating whether the event is an omp extension event.</summary>
    public required bool IsOmpEvent { get; init; }

    /// <summary>Gets how the hook decides: <c>command</c> or <c>prompt</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the command line of a command hook, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Command { get; init; }

    /// <summary>Gets the argument list of a command hook that has one, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>Gets the matcher text, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Matcher { get; init; }

    /// <summary>Gets why the matcher is invalid, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MatcherProblem { get; init; }

    /// <summary>Gets the free-text description, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>Gets a value indicating whether the file leaves the hook on.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Gets a value indicating whether a failure of the hook blocks the action.</summary>
    public required bool FailClosed { get; init; }

    /// <summary>Gets a value indicating whether the hook guards an action.</summary>
    public required bool Blocking { get; init; }

    /// <summary>Gets a value indicating whether the hook needs no consent because a tamper-resistant managed file declares it.</summary>
    public required bool PreConsented { get; init; }

    /// <summary>Gets the hook timeout in seconds.</summary>
    public required double TimeoutSeconds { get; init; }

    /// <summary>Gets the directory the hook would run in, relative to the workspace folder or <c>~/</c> relative.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the file the hook came from, relative to the workspace folder or <c>~/</c> relative.</summary>
    public required string SourceFile { get; init; }
}
