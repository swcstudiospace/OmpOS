namespace OmpOS.Contracts.Catalog;

/// <summary>
/// The answer of the anonymous <c>GET /source</c>: where the corresponding source of this running service is, as the AGPL-3.0 section 13
/// source offer requires.
/// </summary>
public sealed record SourceInfo
{
    /// <summary>Gets the SPDX identifier of the licence.</summary>
    public required string Licence { get; init; }

    /// <summary>Gets the address of the licence text.</summary>
    public required string LicenceUrl { get; init; }

    /// <summary>Gets the address of the source repository.</summary>
    public required string SourceUrl { get; init; }

    /// <summary>Gets the source revision this build was made from, or <c>unknown</c> when the build did not record one.</summary>
    public required string Revision { get; init; }

    /// <summary>Gets the assembly version of the service.</summary>
    public required string Version { get; init; }
}
