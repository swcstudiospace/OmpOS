namespace Autonogrammer.Native.Tests;

/// <summary>
/// The smallest pointer target any interactive control may have. Phase 50 (OQ-87) replaced the 44 px target of the Phase 20 and 46
/// specs by the WCAG 2.2 AA minimum, so the desktop shell can be as dense as the Cursor Agents window. Tests assert this one constant,
/// never a literal number, so a later change of the contract is one edit.
/// </summary>
internal static class TargetSize
{
    /// <summary>The WCAG 2.2 level AA minimum target size in CSS pixels, Success Criterion 2.5.8 (Target Size, Minimum): 24 by 24.</summary>
    public const double MinimumTarget = 24;
}
