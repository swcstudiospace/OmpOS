using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Runs;

/// <summary>
/// One file edit a run's harness reported: the path with the text before and after the edit. This is the harness's own before and after text, not a unified diff or a git patch.
/// </summary>
public sealed record RunFileChange
{
    /// <summary>Gets the path of the edited file, as the harness reported it.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the file text before the edit, or null when the edit created the file.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OldText { get; init; }

    /// <summary>Gets the file text after the edit.</summary>
    public required string NewText { get; init; }
}
