namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// One line of <c>runs.jsonl</c>: a <see cref="HookRunRecord"/> with plain JSON types. It holds no payload body, only the
/// SHA-256 of the input.
/// </summary>
internal sealed class HookRunLogLine
{
    public DateTimeOffset At { get; set; }

    public string SessionId { get; set; } = string.Empty;

    public string? GenerationId { get; set; }

    public string Event { get; set; } = string.Empty;

    public string HookId { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string? MatchedValue { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Decision { get; set; } = string.Empty;

    public int? ExitCode { get; set; }

    public double DurationMs { get; set; }

    public string? UserMessage { get; set; }

    public string? StandardOutput { get; set; }

    public string? StandardError { get; set; }

    public bool OutputTruncated { get; set; }

    public string? PayloadSha256 { get; set; }

    public string? Note { get; set; }
}
