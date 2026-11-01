namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>Application tier. The model cannot create a grant.</summary>
public enum ComputerGrantTier
{
    /// <summary>Screenshot, zoom and window listing.</summary>
    ViewOnly = 0,

    /// <summary>View plus pointer move, click, drag and scroll. No keyboard and no clipboard.</summary>
    ClickOnly = 1,

    /// <summary>Click plus type, chords and clipboard.</summary>
    Full = 2,
}

