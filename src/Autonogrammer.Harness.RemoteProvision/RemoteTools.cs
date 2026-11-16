namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>Which tools the install scripts need. Each flag is true when the tool resolves on the remote path.</summary>
/// <param name="Bash">The scripts run under bash.</param>
/// <param name="Curl">Downloads.</param>
/// <param name="Tar">Unpacks the Omp tarball.</param>
/// <param name="Unzip">Unpacks the Bun archive.</param>
/// <param name="Git">Clones plugin repositories.</param>
/// <param name="Sha256">Either <c>sha256sum</c> or <c>shasum</c>.</param>
/// <param name="Sha512">Either <c>sha512sum</c> or <c>shasum</c>.</param>
public sealed record RemoteTools(bool Bash, bool Curl, bool Tar, bool Unzip, bool Git, bool Sha256, bool Sha512);
