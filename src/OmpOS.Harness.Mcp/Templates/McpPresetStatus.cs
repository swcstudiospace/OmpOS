namespace OmpOS.Harness.Mcp.Templates;

/// <summary>Whether a preset's address and command were confirmed against the vendor's own documentation.</summary>
public enum McpPresetStatus
{
    /// <summary>The address (or command) is stated by the vendor's own documentation; <see cref="McpPreset.VerifiedOn"/> says when it was read.</summary>
    Verified = 0,

    /// <summary>The address could not be confirmed from the vendor's documentation. An unverified preset is never part of <see cref="McpTemplateCatalog.Default"/>.</summary>
    Unverified = 1,
}
