namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>Per-user settings the head overlays onto <see cref="ComputerUseOptions"/>. This assembly does not apply the overlay.</summary>
public sealed class ComputerUseUserSettings
{
    /// <summary>Kill chord override, or <see langword="null"/> to keep the options value.</summary>
    public string? KillChord { get; set; }

    /// <summary>Security phrase shown on the prompt and the indicator.</summary>
    public string? SecurityPhrase { get; set; }

    /// <summary>Whether audit thumbnails are stored. Default off when <see langword="null"/>.</summary>
    public bool? AuditThumbnails { get; set; }

    /// <summary>User denylist additions. Built-in rows are not in this list.</summary>
    public IReadOnlyList<string> DenylistAdditions { get; set; } = [];
}

