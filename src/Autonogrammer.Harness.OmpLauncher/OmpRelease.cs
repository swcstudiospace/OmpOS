namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// One published omp version as the registry describes it, ready to install.
/// </summary>
/// <param name="Version">The plain semantic version.</param>
/// <param name="TarballUri">The https address of the package tarball.</param>
/// <param name="Sha512Hex">The hex SHA-512 of the tarball, decoded from the registry's sha512 integrity value.</param>
/// <param name="EnginesBun">The <c>engines.bun</c> range the release declares, or null when it declares none.</param>
public sealed record OmpRelease(string Version, Uri TarballUri, string Sha512Hex, string? EnginesBun);
