using System.Security.Cryptography;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>What to download, where to keep the partial file, and the digest the body must match.</summary>
/// <param name="Uri">The address to fetch.</param>
/// <param name="PartPath">The partial file. It is deleted unless the download verifies.</param>
/// <param name="Algorithm">The digest algorithm.</param>
/// <param name="ExpectedHex">The expected digest, in hex.</param>
internal sealed record OmpDownloadSpec(Uri Uri, string PartPath, HashAlgorithmName Algorithm, string ExpectedHex);
