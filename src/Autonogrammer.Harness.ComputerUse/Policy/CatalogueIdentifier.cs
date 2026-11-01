namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>One catalogue identifier. Windows and macOS rows are unverified (OQ-388).</summary>
public sealed class CatalogueIdentifier
{
    /// <summary>Platform name: linux, windows or macos.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Opaque identity compared by the store.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the identifier has not been checked on a runner.</summary>
    public bool Unverified { get; set; }
}
