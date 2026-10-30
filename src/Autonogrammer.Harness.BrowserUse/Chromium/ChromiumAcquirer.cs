using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;

namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>
/// Downloads a pinned Chromium archive only after the user confirms. Production accepts TLS
/// <c>storage.googleapis.com</c> only, refuses a cross-host redirect, checks size and SHA-256 before extract, rejects
/// traversal, sets the execute bit and publishes by rename. No row means <see cref="AcquireOutcome.ManagedBrowserNotPinned"/>.
/// </summary>
public sealed class ChromiumAcquirer
{
    /// <summary>The only host a production download may use.</summary>
    public const string ProductionHost = "storage.googleapis.com";

    private readonly HttpMessageHandler _handler;
    private readonly string _allowedHost;
    private readonly bool _requireTls;

    /// <summary>Initializes an acquirer that talks only to <see cref="ProductionHost"/> over TLS.</summary>
    /// <param name="handler">The HTTP handler. Redirects are not followed by this type.</param>
    public ChromiumAcquirer(HttpMessageHandler handler)
        : this(handler, ProductionHost, requireTls: true)
    {
    }

    /// <summary>Initializes an acquirer with an explicit host. Tests pass the loopback listener host.</summary>
    /// <param name="handler">The HTTP handler.</param>
    /// <param name="allowedHost">The only host that may be contacted.</param>
    /// <param name="requireTls">Whether the URL must be HTTPS. Production passes true.</param>
    public ChromiumAcquirer(HttpMessageHandler handler, string allowedHost, bool requireTls)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrEmpty(allowedHost);
        _handler = handler;
        _allowedHost = allowedHost;
        _requireTls = requireTls;
    }

    /// <summary>Returns whether the install action may be shown. An empty manifest hides it.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="platform">The platform id.</param>
    /// <returns>True when a row exists.</returns>
    public static bool IsAcquisitionVisible(ChromiumManifest manifest, string platform)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest.RowFor(platform) is not null;
    }

    /// <summary>Acquires the platform row into <paramref name="stateDirectory"/> when the user confirmed.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="platform">The platform id.</param>
    /// <param name="stateDirectory">The head state directory.</param>
    /// <param name="userConfirmed">Whether the confirmation dialog was accepted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome. The published directory exists only after a successful rename.</returns>
    public async Task<AcquireResult> AcquireAsync(
        ChromiumManifest manifest,
        string platform,
        string stateDirectory,
        bool userConfirmed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrEmpty(platform);
        ArgumentException.ThrowIfNullOrEmpty(stateDirectory);
        var row = manifest.RowFor(platform);
        if (row is null)
        {
            return new AcquireResult(AcquireOutcome.ManagedBrowserNotPinned, null);
        }

        if (!userConfirmed)
        {
            return new AcquireResult(AcquireOutcome.NotConfirmed, null);
        }

        var downloaded = await DownloadVerifiedAsync(row, cancellationToken).ConfigureAwait(false);
        if (downloaded.Failure is not null)
        {
            return downloaded.Failure;
        }

        return Publish(downloaded.Bytes!, row, stateDirectory);
    }

    private async Task<(byte[]? Bytes, AcquireResult? Failure)> DownloadVerifiedAsync(ChromiumManifestRow row, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(row.Source, UriKind.Absolute, out var uri) || !HostAllowed(uri))
        {
            return (null, new AcquireResult(AcquireOutcome.RefusedHost, null));
        }

        using var client = new HttpClient(_handler, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (IsRedirect(response.StatusCode))
        {
            var location = response.Headers.Location;
            if (location is null || !HostAllowed(location.IsAbsoluteUri ? location : new Uri(uri, location)))
            {
                return (null, new AcquireResult(AcquireOutcome.RefusedHost, null));
            }

            return (null, new AcquireResult(AcquireOutcome.RefusedHost, null));
        }

        if (!response.IsSuccessStatusCode || row.SizeBytes < 1)
        {
            return (null, new AcquireResult(response.IsSuccessStatusCode ? AcquireOutcome.RefusedSize : AcquireOutcome.RefusedHost, null));
        }

        if (response.Content.Headers.ContentLength is long declared && declared != row.SizeBytes)
        {
            return (null, new AcquireResult(AcquireOutcome.RefusedSize, null));
        }

        var bytes = await ReadBoundedAsync(response.Content, row.SizeBytes, cancellationToken).ConfigureAwait(false);
        if (bytes.Length != row.SizeBytes)
        {
            return (null, new AcquireResult(AcquireOutcome.RefusedSize, null));
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(hash, row.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return (null, new AcquireResult(AcquireOutcome.RefusedHash, null));
        }

        return (bytes, null);
    }

    private static AcquireResult Publish(byte[] bytes, ChromiumManifestRow row, string stateDirectory)
    {
        var finalDirectory = Path.Combine(stateDirectory, "browser", "chromium", row.Version);
        var staging = Path.Combine(stateDirectory, "browser", "chromium", ".incoming-" + row.Version);
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
        if (!TryExtract(bytes, staging))
        {
            return new AcquireResult(AcquireOutcome.RefusedTraversal, null);
        }

        SetExecuteBit(staging);
        Directory.CreateDirectory(Path.GetDirectoryName(finalDirectory)!);
        Directory.Move(staging, finalDirectory);
        return new AcquireResult(AcquireOutcome.Installed, finalDirectory);
    }

    private bool HostAllowed(Uri uri)
    {
        if (_requireTls && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(uri.IdnHost, _allowedHost, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedirect(HttpStatusCode status)
    {
        return status is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Redirect
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, long expected, CancellationToken cancellationToken)
    {
        var limit = checked((int)Math.Min(expected + 1, int.MaxValue));
        var body = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            while (buffer.Length <= expected)
            {
                var read = await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                var room = limit - (int)buffer.Length;
                await buffer.WriteAsync(chunk.AsMemory(0, Math.Min(read, room)), cancellationToken).ConfigureAwait(false);
                if (buffer.Length > expected)
                {
                    break;
                }
            }

            return buffer.ToArray();
        }
        finally
        {
            await body.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool TryExtract(byte[] archive, string staging)
    {
        Stream payload = new MemoryStream(archive, writable: false);
        if (archive.Length >= 2 && archive[0] == 0x1f && archive[1] == 0x8b)
        {
            payload = new GZipStream(payload, CompressionMode.Decompress);
        }

        using (payload)
        {
            using var reader = new TarReader(payload);
            while (reader.GetNextEntry() is { } entry)
            {
                if (!TrySafePath(staging, entry.Name, out var destination))
                {
                    Directory.Delete(staging, recursive: true);
                    return false;
                }

                if (entry.EntryType is TarEntryType.Directory or TarEntryType.DirectoryList)
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                if (entry.EntryType is not TarEntryType.RegularFile and not TarEntryType.V7RegularFile)
                {
                    Directory.Delete(staging, recursive: true);
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var output = File.Create(destination);
                entry.DataStream?.CopyTo(output);
            }
        }

        return true;
    }

    private static bool TrySafePath(string root, string entryName, out string destination)
    {
        destination = string.Empty;
        if (string.IsNullOrEmpty(entryName) || entryName.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(entryName))
        {
            return false;
        }

        var combined = Path.GetFullPath(Path.Combine(root, entryName.Replace('/', Path.DirectorySeparatorChar)));
        var fullRoot = Path.GetFullPath(root);
        var prefix = fullRoot + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.Ordinal) && !string.Equals(combined, fullRoot, StringComparison.Ordinal))
        {
            return false;
        }

        destination = combined;
        return true;
    }

    private static void SetExecuteBit(string directory)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (!string.Equals(name, "chrome", StringComparison.Ordinal) && !string.Equals(name, "chromium", StringComparison.Ordinal))
            {
                continue;
            }

            var mode = File.GetUnixFileMode(file);
            File.SetUnixFileMode(file, mode | UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
