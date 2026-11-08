using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Makes the pinned Bun available. The archive is downloaded from GitHub and its SHA-256 is checked before anything is
/// extracted. Only the single Bun executable is taken from the archive, written to a fixed path.
/// </summary>
internal sealed class OmpBunProvisioner
{
    private const UnixFileMode ExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private readonly OmpManagedOptions _options;
    private readonly OmpManagedLayout _layout;
    private readonly OmpVerifiedDownloader _downloader;

    /// <summary>Creates the provisioner.</summary>
    /// <param name="options">The managed options, which hold the pin, the platform and the GitHub base.</param>
    /// <param name="layout">The managed layout.</param>
    /// <param name="downloader">The verified downloader.</param>
    public OmpBunProvisioner(OmpManagedOptions options, OmpManagedLayout layout, OmpVerifiedDownloader downloader)
    {
        _options = options;
        _layout = layout;
        _downloader = downloader;
    }

    /// <summary>Returns the path of the pinned Bun executable, downloading and verifying it when it is not present.</summary>
    /// <param name="state">The state read before the attempt.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The executable path, or the failure that ends the install.</returns>
    public async Task<OmpStep<string>> EnsureAsync(OmpManagedState state, CancellationToken cancellationToken)
    {
        var pin = _options.BunPin;
        var platform = _options.PlatformKey;
        var asset = pin.AssetFor(platform);
        if (platform is null || asset is null)
        {
            return OmpStep<string>.Fail(OmpResults.Failed(OmpFailureKind.UnsupportedPlatform, null, OmpManagedMessages.UnsupportedPlatform(platform)));
        }

        var executable = Path.Combine(_layout.BunDirectory(pin.Version), OmpBunPin.ExecutableName(platform));
        if (File.Exists(executable))
        {
            return OmpStep<string>.Ok(executable);
        }

        var uri = new Uri(_options.GitHubBaseUri, "oven-sh/bun/releases/download/bun-v" + pin.Version + "/" + asset.FileName);
        Directory.CreateDirectory(_layout.DownloadsDirectory);
        var part = Path.Combine(_layout.DownloadsDirectory, "bun-" + pin.Version + "-" + platform + ".zip.part");
        try
        {
            var status = await _downloader.DownloadAsync(new OmpDownloadSpec(uri, part, HashAlgorithmName.SHA256, asset.Sha256), cancellationToken).ConfigureAwait(false);
            var failure = OmpResults.ForDownload(status, "Bun " + pin.Version, null, state);
            if (failure is not null)
            {
                return OmpStep<string>.Fail(failure);
            }

            return ExtractExecutable(part, executable, platform);
        }
        finally
        {
            OmpCleanup.DeleteFile(part);
        }
    }

    private OmpStep<string> ExtractExecutable(string archive, string executable, string platform)
    {
        var name = OmpBunPin.ExecutableName(platform);
        using var zip = ZipFile.OpenRead(archive);
        var entry = zip.Entries.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
        if (entry is null)
        {
            return OmpStep<string>.Fail(OmpResults.Failed(OmpFailureKind.BunUnavailable, null, OmpManagedMessages.BunMissingExecutable(_options.BunPin.Version)));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(executable) ?? _layout.BaseDirectory);
        var temporary = executable + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp";
        var moved = false;
        try
        {
            using (var source = entry.Open())
            using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(target);
            }

            File.Move(temporary, executable, overwrite: true);
            moved = true;
        }
        finally
        {
            if (!moved)
            {
                OmpCleanup.DeleteFile(temporary);
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(executable, ExecutableMode);
        }

        return OmpStep<string>.Ok(executable);
    }
}
