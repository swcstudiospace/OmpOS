using System.Text.Json;

namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Owner-only atomic store of which installed bundles are enabled.</summary>
public sealed class BundleStateStore
{
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="BundleStateStore"/> class.</summary>
    /// <param name="path">The full path of <c>plugins-state.json</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    public BundleStateStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <summary>Reads whether a bundle is enabled.</summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="enabled">The stored flag when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the file names the bundle.</returns>
    public bool TryGetEnabled(string bundleId, out bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleId);
        lock (_gate)
        {
            var document = Read();
            if (document.Bundles.TryGetValue(bundleId, out var entry))
            {
                enabled = entry.Enabled;
                return true;
            }
        }

        enabled = false;
        return false;
    }

    /// <summary>Stores whether a bundle is enabled and rewrites the file atomically.</summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="enabled">The new flag.</param>
    public void SetEnabled(string bundleId, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleId);
        lock (_gate)
        {
            var document = Read();
            document.Bundles[bundleId] = new BundleStateEntry { Enabled = enabled };
            Write(document);
        }
    }

    private BundleStateDocument Read()
    {
        if (!File.Exists(_path))
        {
            return new BundleStateDocument();
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize(json, BundleStateJsonContext.Default.BundleStateDocument) ?? new BundleStateDocument();
    }

    private void Write(BundleStateDocument document)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(directory, OwnerDirectory);
            }
        }

        var temporary = _path + ".tmp";
        var json = JsonSerializer.Serialize(document, BundleStateJsonContext.Default.BundleStateDocument);
        File.WriteAllText(temporary, json);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(temporary, OwnerFile);
        }

        File.Move(temporary, _path, overwrite: true);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(_path, OwnerFile);
        }
    }
}
