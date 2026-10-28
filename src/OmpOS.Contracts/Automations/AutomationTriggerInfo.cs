using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Automations;

/// <summary>One trigger of an automation. The automation runs when any of its triggers fires.</summary>
public sealed record AutomationTriggerInfo
{
    /// <summary>Gets the kind of trigger: <c>cron</c> or <c>webhook</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Gets the five-field cron expression of a cron trigger, for example <c>0 9 * * mon-fri</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cron { get; init; }

    /// <summary>Gets the sender a webhook trigger accepts: <c>generic</c> (default) or <c>github</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Flavour { get; init; }

    /// <summary>Gets the GitHub events a webhook accepts: <c>push</c>, <c>pull_request</c>. Empty accepts both.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Events { get; init; }

    /// <summary>Gets the pull request actions a GitHub webhook accepts, for example <c>opened</c>, <c>synchronize</c>, <c>merged</c>. Empty accepts every action.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Actions { get; init; }

    /// <summary>Gets the branch globs a GitHub webhook accepts (the pushed branch or the pull request's base branch). Empty accepts every branch.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Branches { get; init; }
}
