using System.Text.Json;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// The result of parsing one hook's standard output. Fields the event does not honour are already dropped.
/// </summary>
public sealed record HookOutputParse
{
    /// <summary>How the output parsed.</summary>
    public HookOutputKind Kind { get; init; }

    /// <summary>Why the output is invalid, or null.</summary>
    public string? Problem { get; init; }

    /// <summary>The permission the hook answered, or null.</summary>
    public HookVerdict? Verdict { get; init; }

    /// <summary>The user message.</summary>
    public string? UserMessage { get; init; }

    /// <summary>The agent message.</summary>
    public string? AgentMessage { get; init; }

    /// <summary>The replacement tool input.</summary>
    public JsonElement? UpdatedInput { get; init; }

    /// <summary>The replacement MCP tool output.</summary>
    public JsonElement? UpdatedMcpToolOutput { get; init; }

    /// <summary>The additional context.</summary>
    public string? AdditionalContext { get; init; }

    /// <summary>The follow-up message.</summary>
    public string? FollowupMessage { get; init; }

    /// <summary>The session environment of a <c>sessionStart</c> hook.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The <c>continue</c> answer.</summary>
    public bool? ShouldContinue { get; init; }

    /// <summary>The plugin directories of a <c>workspaceOpen</c> hook.</summary>
    public IReadOnlyList<string> PluginPaths { get; init; } = [];

    /// <summary>The whole output object of an <c>omp</c> section hook.</summary>
    public JsonElement? OmpObject { get; init; }

    /// <summary>The output of a hook that said nothing.</summary>
    public static HookOutputParse EmptyOutput { get; } = new() { Kind = HookOutputKind.Empty };

    /// <summary>
    /// Creates the result for an output that cannot be used.
    /// </summary>
    /// <param name="problem">What is wrong.</param>
    /// <returns>An invalid result.</returns>
    public static HookOutputParse Failure(string problem) => new() { Kind = HookOutputKind.Invalid, Problem = problem };
}
