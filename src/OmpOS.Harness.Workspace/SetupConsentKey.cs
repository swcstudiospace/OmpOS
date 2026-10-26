namespace OmpOS.Workspace;

/// <summary>
/// What a consent answer applies to: one repository and the exact bytes of its setup file. A different repository or a
/// changed file is a different key, so it needs its own consent.
/// </summary>
/// <param name="RepositoryPath">The canonical (real, absolute) path of the repository root.</param>
/// <param name="SetupSha256">The lowercase hexadecimal SHA-256 of the bytes of <c>.ompos/setup.json</c>.</param>
public sealed record SetupConsentKey(string RepositoryPath, string SetupSha256);
